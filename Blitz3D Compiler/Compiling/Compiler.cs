using System;
using System.IO;
using Blitz3D.Compiling.ASM.x86;

namespace Blitz3D.Compiling
{
	public class Compiler
	{
		private static void showInfo()
		{
			const int BASE_VER = 1108;
			const int PRO_F = 0x010000;
			const int VERSION = BASE_VER|PRO_F;
			const int major = (VERSION & 0xffff) / 100;
			const int minor = (VERSION & 0xffff) % 100;

			Console.WriteLine($"BlitzCC V{major}.{minor}");
			Console.WriteLine("(C)opyright 2000-2003 Blitz Research Ltd");
		}

		private readonly FileInfo in_file;
		private readonly FileInfo out_file;
		private readonly bool debug;
		private readonly bool quiet;

		public Compiler(FileInfo input, FileInfo output, bool debug, bool quiet = false)
		{
			in_file = input;
			out_file = output;
			this.debug = debug;
			this.quiet = quiet;
		}

		public void Compile()
		{
			if(!quiet)
			{
				showInfo();
				Console.WriteLine($"Compiling \"{in_file}\"");
			}

			Libs.openLibs();

			Libs.linkRuntime();
			Libs.linkUserLibs();

			try
			{
				//parse
				using StreamReader input = new StreamReader(in_file.OpenRead());
				if(!quiet) Console.WriteLine("Parsing...");
				Toker toker = new Toker(input);
				Parser parser = new Parser(toker);
				ProgNode prog = parser.parse(in_file.FullName);

				//semant
				if(!quiet) Console.WriteLine("Generating...");
				Environ environ_ = prog.semant(Libs.runtimeEnviron);

				//translate
				if(!quiet) Console.WriteLine("Translating...");
				StringWriter asmcode = new StringWriter();
				asmcode.NewLine = "\n";
				Codegen_x86 codegen = new Codegen_x86(asmcode, debug);

				prog.translate(codegen, Libs.userFuncs);

				try
				{
					File.WriteAllText(out_file.FullName, asmcode.ToString());
				}
				catch(Exception e)
				{
					Console.Error.WriteLine($"Failed to write output to '{out_file}'.");
					Console.Error.WriteLine(e);
					return;
				}
			}
			catch(Ex x)
			{
				string file = '\"' + x.file + '\"';
				int row = ((x.pos >> 16) & 65535) + 1, col = (x.pos & 65535) + 1;
				Console.WriteLine(file + ":" + row + ":" + col + ":" + row + ":" + col + ":" + x.ex);
				return;
			}
		}
	}
}