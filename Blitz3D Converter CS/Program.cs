using System;
using System.Collections.Generic;
using System.IO;
using Blitz3D.Converter.Parsing;
using Blitz3D.Converter.Parsing.Nodes;

namespace Blitz3D.Converter
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
				output = new FileInfo(Path.ChangeExtension(input.FullName, ".cs"));
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

				Console.WriteLine("Converting...");
				string output = Convert(prog.WriteData());

				try
				{
					Console.WriteLine("Saving...");
					File.WriteAllText(fileOut.FullName, output);
				}
				catch(Exception e)
				{
					Console.Error.WriteLine($"Failed to write output to '{fileOut}'.");
					Console.Error.WriteLine(e);
					return;
				}
				ConvertIncludes(parser);
			}
			catch(Ex x)
			{
				Console.WriteLine(x.Message);
			}
		}

		private static (ProgNode prog, Parser parser) Parse(FileInfo inputFile)
		{
			using StreamReader input = new StreamReader(inputFile.OpenRead());
			Tokenizer toker = new Tokenizer(input, inputFile.FullName);
			Parser parser = new Parser(toker);
			return (parser.parse(), parser);
		}

		private static void Semant(ProgNode prog, Libs libs)
		{
			prog.Semant(libs.RuntimeEnviron);
		}

		private static string Convert(IEnumerable<string> prog)
		{
			StringWriter output = new StringWriter{NewLine = "\n"};
			int indent = 0;
			foreach(string str in prog)
			{
				if(str == "}"){indent--;}
				output.Write(new string('\t',indent));
				output.WriteLine(str);
				if(str == "{"){indent++;}
			}
			return output.ToString();
		}

		private static void ConvertIncludes(Parser parser)
		{
			foreach(var include in parser.Included)
			{
				FileInfo fileOut = new FileInfo(Path.ChangeExtension(include.Key, ".cs"));
				string output = Convert(include.Value.WriteData());
				File.WriteAllText(fileOut.FullName, output);
			}
		}
	}
}
