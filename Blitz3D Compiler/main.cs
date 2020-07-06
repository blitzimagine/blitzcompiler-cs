//#pragma warning(disable:4786)

//#include <iostream>

//#include "config/config.h"
//#include "stdutil/stdutil.h"

//#include <map>
//#include <list>
//#include <string>
//#include <vector>
//#include <fstream>
//#include <iostream>
//#include <iomanip>
//#include "blitz/libs.h"

//using namespace std;

//#include "linker/linker.h"
//#include "compiler/environ.h"
//#include "compiler/parser.h"
//#include "compiler/codegen_x86/codegen_x86.h"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using codegen_86;

public static class main
{
	private static void showInfo()
	{
		const int major = (config.VERSION & 0xffff) / 100;
		const int minor = (config.VERSION & 0xffff) % 100;
		Console.WriteLine($"BlitzCC V{major}.{minor}");
		Console.WriteLine("(C)opyright 2000-2003 Blitz Research Ltd");
	}

	private static readonly (string flag,string description)[] flags =
	{
		("-h","show this help"),
		("-q","quiet mode"),
		("+q","very quiet mode"),
		//("-c",null),//Listed in the usage but not in the help not implemented in the code :/
		("-d","debug compile"),
		("-k","dump keywords"),
		("+k","dump keywords and syntax"),
		("-v","version info")
	};

	private static void showUsage()
	{
		Console.WriteLine($"Usage: bbc [{string.Join('|', flags.Select(f => f.flag))}] [sourcefile.bb] [outputfile.asm]");
	}

	private static void showHelp()
	{
		showUsage();
		foreach(var flag in flags)
		{
			Console.WriteLine($"{flag.flag}         : {flag.description}");
		}
	}

	private static string quickHelp(string kw)
	{
		Environ e = libs.runtimeEnviron;
		Decl d = e.funcDecls.findDecl(kw.ToLowerInvariant());
		if(d is null || d.type.funcType() == null) return "No quick help available for " + kw;
		string t = kw;
		FuncType f = d.type.funcType();
		if(f.returnType == Type.float_type) t += '#';
		else if(f.returnType == Type.string_type) t += '$';

		t += " ";

		if(f.returnType != Type.void_type) t += "( ";

		for(int k = 0; k < f.@params.size(); ++k)
		{
			string s = string.Empty;
			if(k!=0) s += ',';
			Decl p = f.@params.decls[k];
			s += p.name;
			if(p.type == Type.float_type) s += '#';
			else if(p.type == Type.string_type) s += '$';
			else if(p.type == Type.void_type) s += '*';
			if(p.defType!=null) s = '[' + s + ']';
			t += s;
		}

		if(f.returnType != Type.void_type)
		{
			t += f.@params.size()!=0 ? " )" : ")";
		}
		return t;
	}

	private static void dumpKeys(bool lang, bool mod, bool help)
	{
		if(lang)
		{
			Dictionary<string, int> keywords = Toker.getKeywords();
			foreach(var entry in keywords)
			{
				if(entry.Key.Contains(' ')) continue;
				Console.WriteLine(entry.Key);
			}
		}

		if(!mod) return;

		for(int k = 0; k < (int)libs.keyWords.Count; ++k)
		{
			string t = libs.keyWords[k];

			if(t[0] == '_') continue;
			if(!char.IsLetter(t[0])) t = t.Substring(1);
			for(int n = 0; n < t.Length; ++n)
			{
				if(!char.IsLetterOrDigit(t[n]) && t[n] != '_')
				{
					t = t.Substring(0, n);
					break;
				}
			}
			if(help) t = quickHelp(t);
			Console.WriteLine(t);
		}
	}

	private static string verstr(int ver)
	{
		return /*itoa*/((ver & 65535) / 100).ToString() + "." + /*itoa*/((ver & 65535) % 100).ToString();
	}

	private static void versInfo()
	{
		Console.WriteLine("Compiler version:" + verstr(libs.bcc_ver));
		Console.WriteLine("Runtime version:" + verstr(libs.run_ver));
		Console.WriteLine("Debugger version:" + verstr(libs.dbg_ver));
		Console.WriteLine("Linker version:" + verstr(libs.lnk_ver));
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
		bool dumpkeys = false, dumphelp = false, showhelp = false;
		bool versinfo = false;

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
					case "+k":
						dumphelp = true;
						goto case "-k";
					case "-k":
						dumpkeys = true;
						break;
					case "-v":
						versinfo = true;
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

		if(libs.openLibs() is string er1)
		{
			Console.Error.WriteLine(er1);
			return -1;
		}
		if(libs.linkLibs() is string er2)
		{
			Console.Error.WriteLine(er2);
			return -1;
		}

		if(showhelp) showHelp();
		if(dumpkeys) dumpKeys(true, true, dumphelp);
		if(versinfo) versInfo();

		//if(in_file[0] == '\"')
		//{
		//	if(in_file.Length < 3 || in_file[in_file.Length - 1] != '\"') usageError();
		//	in_file = in_file.Substring(1, in_file.Length - 2);
		//}

		if(out_file is null || !out_file.Exists)
		{
			//bool foundExtension = false;
			//for(int i = in_file.Length - 1; i >= 0; i--)
			//{
			//	char c = in_file[i];
			//	if(c == '.')
			//	{
			//		foundExtension = true;
			//		continue;
			//	}

			//	if(foundExtension)
			//	{
			//		out_file += c;
			//	}
			//}
			//if(foundExtension)
			//	out_file = new string(out_file.Reverse().ToArray());//reverse(out_file.begin(), out_file.end());

			//if(out_file.Length==0)
			//	out_file = in_file;

			out_file = new FileInfo(Path.ChangeExtension(in_file.FullName, ".asm"));
		}

		if(!quiet)
		{
			showInfo();
			Console.WriteLine($"Compiling \"{in_file}\"");
		}

		using StreamReader @in = new StreamReader(in_file.OpenRead());//ifstream

		//int n = in_file.LastIndexOf('/');
		//if(n == -1)
		//{
		//	n = in_file.LastIndexOf('\\');
		//}
		//if(n != -1)
		//{
		//	if(n==0 || in_file[n - 1] == ':')
		//	{
		//		++n;
		//	}
		//	Directory.SetCurrentDirectory(in_file.Directory.FullName);
		//}
		Directory.SetCurrentDirectory(in_file.Directory.FullName);
		ProgNode prog;
		Environ environ_;
		//Module module;

		try
		{
			//parse
			if(!veryquiet) Console.WriteLine("Parsing...");
			Toker toker = new Toker(@in);
			Parser parser = new Parser(toker);
			prog = parser.parse(in_file.FullName);

			//semant
			if(!veryquiet) Console.WriteLine("Generating...");
			environ_ = prog.semant(libs.runtimeEnviron);

			//translate
			if(!veryquiet) Console.WriteLine("Translating...");
			//StringWriter qbuf = new StringWriter();//qstreambuf
			StringWriter asmcode = new StringWriter(/*qbuf*/);//iostream
			Codegen_x86 codegen = new Codegen_x86(asmcode, debug);

			prog.translate(codegen, libs.userFuncs);

			string asmCodeStr = asmcode.ToString();
			if(out_file.Length==0)
			{
				Console.WriteLine('\n' + asmCodeStr);
			}
			else
			{
				try
				{
					File.WriteAllText(out_file.FullName, asmCodeStr);
					//using StreamWriter @out = new StreamWriter(out_file);//ofstream
					//@out.Write(asmCodeStr);
					//@out.Flush();
				}
				catch(Exception e)
				{
					Console.Error.WriteLine($"Failed to write output to '{out_file}'.\n{e}");
					return -1;
				}

			}
		}
		catch(Ex x)
		{
			string file = '\"' + x.file + '\"';
			int row = ((x.pos >> 16) & 65535) + 1, col = (x.pos & 65535) + 1;
			Console.WriteLine(file + ":" + row + ":" + col + ":" + row + ":" + col + ":" + x.ex);
			return -1;
		}

		//delete prog;

		//delete module;
		//delete environ_;

		libs.closeLibs();

		return 0;
	}
}