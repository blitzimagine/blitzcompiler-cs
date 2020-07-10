#define CONVERT
using System;
using System.IO;
using Blitz3D.Compiling.ASM.x86;
using Blitz3D.Parsing;

namespace Blitz3D
{
	public static class Program
	{
		private static void ShowCompilerInfo()
		{
			Console.WriteLine($"BlitzCC V11.8");//Version 1108
			Console.WriteLine("(C)opyright 2000-2003 Blitz Research Ltd");
		}

		/// <summary>Parse file arguments</summary>
		/// <returns>Returns error message if bad args given.</returns>
		private static string ParseArgs(string[] args, out FileInfo input, out FileInfo output)
		{
			input = null;
			output = null;
			if(args.Length == 0 || args.Length>2)
			{
				return "Usage: bbc [sourcefile.bb] [outputfile.asm]";
			}

			input = new FileInfo(args[0]);
			if(!input.Exists)
			{
				return $"Invalid input file: {input.FullName}";
			}

			if(args.Length>1)
			{
				output = new FileInfo(args[1]);
			}
			else
			{
				#if CONVERT
				output = new FileInfo(Path.ChangeExtension(input.FullName, ".cs"));
				#else
				out_file = new FileInfo(Path.ChangeExtension(in_file.FullName, ".asm"));
				#endif
			}
			Directory.SetCurrentDirectory(input.Directory.FullName);
			return null;
		}

		public static void Main(string[] args)
		{
			if(ParseArgs(args, out FileInfo fileIn, out FileInfo fileOut) is string error)
			{
				Console.WriteLine(error);
				return;
			}

			Console.WriteLine($"Compiling \"{fileIn}\"");

			try
			{
				Libs libs = Libs.InitLibs();

				Console.WriteLine("Parsing...");
				(ProgNode prog, Parser parser) = Parse(fileIn);

				Console.WriteLine("Generating...");
				Semant(prog, libs);

				#if CONVERT
				Console.WriteLine("Converting...");
				string output = Convert(prog);
				#else
				Console.WriteLine("Translating...");
				StringWriter output = Translate(prog, libs);
				#endif

				try
				{
					Console.WriteLine("Saving...");
					File.WriteAllText(fileOut.FullName, output);
				}
				catch(Exception e)
				{
					Console.Error.WriteLine($"Failed to write output to '{fileOut}'.");
					Console.Error.WriteLine(e);
				}
				ConvertInclude(parser);
			}
			catch(Ex x)
			{
				string file = $"\"{x.file}\"";
				int row = ((x.pos >> 16) & 65535) + 1;
				int col = (x.pos & 65535) + 1;
				Console.WriteLine($"{file}:{row}:{col}:{x.ex}");
			}
		}

		private static (ProgNode prog, Parser parser) Parse(FileInfo inputFile)
		{
			using StreamReader input = new StreamReader(inputFile.OpenRead());
			Toker toker = new Toker(input);
			Parser parser = new Parser(toker);
			return (parser.parse(inputFile.FullName), parser);
		}

		private static Environ Semant(ProgNode prog, Libs libs)
		{
			return prog.Semant(libs.runtimeEnviron);
		}

		private static StringWriter Translate(ProgNode prog, Libs libs)
		{
			StringWriter output = new StringWriter{NewLine = "\n"};
			Codegen_x86 codegen = new Codegen_x86(output);
			prog.Translate(codegen, libs.userFuncs);
			return output;
		}

		private static string Convert(Node prog)
		{
			StringWriter output = new StringWriter{NewLine = "\n"};
			int indent = 0;
			foreach(string str in prog.WriteData())
			{
				if(str == "}"){indent--;}
				output.Write(new string('\t',indent));
				output.WriteLine(str);
				if(str == "{"){indent++;}
			}
			return output.ToString();
		}

		private static void ConvertInclude(Parser parser)
		{
			foreach(var include in parser.included)
			{
				FileInfo fileOut = new FileInfo(Path.ChangeExtension(include.Key, ".cs"));
				string output = Convert(include.Value);
				File.WriteAllText(fileOut.FullName, output);
			}
		}
	}
}
