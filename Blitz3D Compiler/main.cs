using System;
using System.IO;
using System.Linq;
using codegen_86;

public static class main
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
		("+q","very quiet mode"),
		("-d","debug compile"),
		("-k","dump keywords"),
		("+k","dump keywords and syntax"),
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

	public static int Main(string[] argp)
	{
		if(argp.Length == 0)
		{
			showUsage();
			return -1;
		}
		FileInfo in_file = null;
		FileInfo out_file = null;

		bool debug = false, quiet = false, veryquiet = false;
		bool showhelp = false;

		for(int k = 0; k < argp.Length; ++k)
		{
			string t = argp[k];

			t = t.ToLowerInvariant();

			if(t[0] == '-' || t[0] == '+')
			{
				switch(t)
				{
					case "-h":
						showhelp = true;
						break;
					case "+q":
						veryquiet = true;
						goto case "-q";
					case "-q":
						quiet = true;
						break;
					case "-d":
						debug = true;
						break;
					default:
						showUsage();
						return -1;
				}
			}
			else if(in_file is null)
			{
				in_file = new FileInfo(argp[k]);
			}
			else if(out_file is null)
			{
				out_file = new FileInfo(argp[k]);
			}
			else
			{
				showUsage();
				return -1;
			}
		}
		if(in_file is null || !in_file.Exists)
		{
			return 0;
		}

		libs.openLibs();

		if(libs.linkLibs() is string er2)
		{
			Console.Error.WriteLine(er2);
			return -1;
		}

		if(showhelp) showUsage(true);
		if(out_file is null || !out_file.Exists)
		{
			out_file = new FileInfo(Path.ChangeExtension(in_file.FullName, ".asm"));
		}

		if(!quiet)
		{
			showInfo();
			Console.WriteLine($"Compiling \"{in_file}\"");
		}

		Directory.SetCurrentDirectory(in_file.Directory.FullName);
		ProgNode prog;
		Environ environ_;

		try
		{
			//parse
			using StreamReader input = new StreamReader(in_file.OpenRead());
			if(!veryquiet) Console.WriteLine("Parsing...");
			Toker toker = new Toker(input);
			Parser parser = new Parser(toker);
			prog = parser.parse(in_file.FullName);

			//semant
			if(!veryquiet) Console.WriteLine("Generating...");
			environ_ = prog.semant(libs.runtimeEnviron);

			//translate
			if(!veryquiet) Console.WriteLine("Translating...");
			StringWriter asmcode = new StringWriter();
			asmcode.NewLine = "\n";
			Codegen_x86 codegen = new Codegen_x86(asmcode, debug);

			prog.translate(codegen, libs.userFuncs);

			try
			{
				File.WriteAllText(out_file.FullName, asmcode.ToString());
			}
			catch(Exception e)
			{
				Console.Error.WriteLine($"Failed to write output to '{out_file}'.");
				Console.Error.WriteLine(e);
				return -1;
			}
		}
		catch(Ex x)
		{
			string file = '\"' + x.file + '\"';
			int row = ((x.pos >> 16) & 65535) + 1, col = (x.pos & 65535) + 1;
			Console.WriteLine(file + ":" + row + ":" + col + ":" + row + ":" + col + ":" + x.ex);
			return -1;
		}
		return 0;
	}
}