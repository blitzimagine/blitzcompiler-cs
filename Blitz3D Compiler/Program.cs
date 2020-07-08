using System;
using System.IO;
using System.Linq;
using Blitz3D.Compiling;
using Blitz3D.Compiling.ASM.x86;
using Blitz3D.Parsing;

namespace Blitz3D
{
	public static class Program
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

		private static readonly (string flag,string description)[] flags =
		{
			("-h","show this help"),
			("-q","quiet mode"),
		};

		private static void showUsage(bool help = false)
		{
			Console.WriteLine($"Usage: bbc [{string.Join('|', flags.Select(f => f.flag))}] [sourcefile.bb] [outputfile.asm]");
			if(help)
			{
				foreach(var flag in flags)
				{
					Console.WriteLine($"{flag.flag} : {flag.description}");
				}
			}
		}

		public static void Main(string[] args)
		{
			if(args.Length == 0)
			{
				showUsage();
				return;
			}
			FileInfo in_file = null;
			FileInfo out_file = null;

			bool quiet = false;
			bool showhelp = false;

			for(int k = 0; k < args.Length; ++k)
			{
				string t = args[k];

				t = t.ToLowerInvariant();

				if(t[0] == '-')
				{
					switch(t)
					{
						case "-h":
							showhelp = true;
							break;
						case "-q":
							quiet = true;
							break;
						default:
							showUsage();
							return;
					}
				}
				else if(in_file is null)
				{
					in_file = new FileInfo(args[k]);
				}
				else if(out_file is null)
				{
					out_file = new FileInfo(args[k]);
				}
				else
				{
					showUsage();
					return;
				}
			}
			if(in_file is null || !in_file.Exists)
			{
				return;
			}

			if(showhelp) showUsage(true);
			if(out_file is null || !out_file.Exists)
			{
				out_file = new FileInfo(Path.ChangeExtension(in_file.FullName, ".asm"));
			}

			Directory.SetCurrentDirectory(in_file.Directory.FullName);

			if(!quiet)
			{
				showInfo();
				Console.WriteLine($"Compiling \"{in_file}\"");
			}

			Libs libs = Libs.InitLibs();

			try
			{
				using StreamReader input = new StreamReader(in_file.OpenRead());
				StringWriter asmcode = new StringWriter{NewLine = "\n"};

				//parse
				if(!quiet)
				{
					Console.WriteLine("Parsing...");
				}
				Toker toker = new Toker(input);
				Parser parser = new Parser(toker);
				ProgNode prog = parser.parse(in_file.FullName);

				//semant
				if(!quiet)
				{
					Console.WriteLine("Generating...");
				}
				Environ environ_ = prog.Semant(libs.runtimeEnviron);

				//translate
				if(!quiet)
				{
					Console.WriteLine("Translating...");
				}
				Codegen_x86 codegen = new Codegen_x86(asmcode);
				prog.Translate(codegen, libs.userFuncs);

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
				string file = $"\"{x.file}\"";
				int row = ((x.pos >> 16) & 65535) + 1;
				int col = (x.pos & 65535) + 1;
				Console.WriteLine($"{file}:{row}:{col}:{x.ex}");
				throw;
			}
		}
	}
}
