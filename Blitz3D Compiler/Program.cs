using System;
using System.IO;
using System.Linq;
using Blitz3D.Compiling;

namespace Blitz3D
{
	public static class Program
	{
		private static readonly (string flag,string description)[] flags =
		{
			("-h","show this help"),
			("-q","quiet mode"),
			("-d","debug compile"),
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

			bool debug = false, quiet = false;
			bool showhelp = false;

			for(int k = 0; k < args.Length; ++k)
			{
				string t = args[k];

				t = t.ToLowerInvariant();

				if(t[0] == '-' || t[0] == '+')
				{
					switch(t)
					{
						case "-h":
							showhelp = true;
							break;
						case "-q":
							quiet = true;
							break;
						case "-d":
							debug = true;
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

			Compiler compiler = new Compiler(in_file, out_file, debug, quiet);
			compiler.Compile();
		}
	}
}
