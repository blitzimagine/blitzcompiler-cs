﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Blitz3D.Parsing;
using Blitz3D.Parsing.Nodes;

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
				}
				ConvertInclude(parser);
			}
			catch(Ex x)
			{
				Console.Write($"\"{x.file}\":");
				if(x.pos is Point pos)
				{
					int row = pos.Y + 1;
					int col = pos.X + 1;
					Console.Write($"{row}:{col}:");
				}
				Console.WriteLine(x.ex);
			}
		}

		private static (ProgNode prog, Parser parser) Parse(FileInfo inputFile)
		{
			using StreamReader input = new StreamReader(inputFile.OpenRead());
			Tokenizer toker = new Tokenizer(input);
			Parser parser = new Parser(toker);
			return (parser.parse(inputFile.FullName), parser);
		}

		private static Environ Semant(ProgNode prog, Libs libs)
		{
			return prog.Semant(libs.runtimeEnviron);
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

		private static void ConvertInclude(Parser parser)
		{
			foreach(var include in parser.included)
			{
				FileInfo fileOut = new FileInfo(Path.ChangeExtension(include.Key, ".cs"));
				string output = Convert(include.Value.WriteData());
				File.WriteAllText(fileOut.FullName, output);
			}
		}
	}
}
