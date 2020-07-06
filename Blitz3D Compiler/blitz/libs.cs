using System.Collections.Generic;
using System.IO;

public static class libs
{
	//openLibs
	private static Runtime runtimeLib;

	//linkLibs
	public static Environ runtimeEnviron;
	public static List<string> keyWords = new List<string>();
	public static List<UserFunc> userFuncs = new List<UserFunc>();

	public static void openLibs()
	{
		runtimeLib = Runtime.runtimeGetRuntime();

		runtimeEnviron = new Environ("", Type.int_type, 0, null);

		keyWords.Clear();
		userFuncs.Clear();
	}

	public static string linkLibs()
	{
		if(linkRuntime() is string p1) return p1;

		if(linkUserLibs() is string p2) return p2;

		return null;
	}

	public static void closeLibs()
	{
		runtimeEnviron = null;
		if(runtimeLib!=null)
		{
			runtimeLib.shutdown();
		}

		runtimeEnviron = null;
	}

	private static Type @typeof(int c)
	{
		switch(c)
		{
			case '%': return Type.int_type;
			case '#': return Type.float_type;
			case '$': return Type.string_type;
		}
		return Type.void_type;
	}

	private static int curr;
	private static string text;

	private static int bnext(StreamReader @in)//istream
	{
		text = "";

		int t;
		for(;;)
		{
			while(char.IsWhiteSpace((char)@in.Peek()))
			{
				@in.Read();
			}
			if(@in.EndOfStream)
			{
				curr = 0;
				return curr;
			}
			t = @in.Read();
			if(t != ';')
			{
				break;
			}
			while(!@in.EndOfStream && @in.Read() != '\n') { }
		}

		if(char.IsLetter((char)t))
		{
			text += (char)t;
			while(char.IsLetterOrDigit((char)@in.Peek()) || @in.Peek() == '_') text += (char)@in.Read();
			curr = -1;
			return curr;
		}
		if(t == '\"')
		{
			while(@in.Peek() != '\"') text += (char)@in.Read();
			@in.Read();
			curr = -2;
			return curr;
		}

		curr = t;
		return curr;
	}

	private static string linkRuntime()
	{
		while(runtimeLib.nextSym() is string sym)
		{
			string s = sym;

			int pc = runtimeLib.symValue(sym);

			//internal?
			if(s[0] == '_')
			{
				continue;
			}

			bool cfunc = false;

			if(s[0] == '!')
			{
				cfunc = true;
				s = s.Substring(1);
			}

			keyWords.Add(s);

			//global!
			int start = 0, end;
			Type t = Type.void_type;
			if(!char.IsLetter(s[0]))
			{
				start = 1;
				t = @typeof(s[0]);
			}
			int k;
			for(k = 1; k < s.Length; ++k)
			{
				if(!char.IsLetterOrDigit(s[k]) && s[k] != '_') break;
			}
			end = k;
			DeclSeq @params = new DeclSeq();
			string n = s.Substring(start, end - start);
			while(k < s.Length)
			{
				Type t2 = @typeof(s[k++]);
				int from = k;
				for(; k<s.Length && (char.IsLetterOrDigit(s[k]) || s[k] == '_'); ++k) { }
				string str = s.Substring(from, k - from);
				ConstType defType = null;
				if(k<s.Length && s[k] == '=')
				{
					int from2 = ++k;
					if(k<s.Length && s[k] == '\"')
					{
						for(++k; s[k] != '\"'; ++k) { }
						string t3 = s.Substring(from2 + 1, k - from2 - 1);
						defType = new ConstType(t3);
						++k;
					}
					else
					{
						if(k<s.Length && s[k] == '-') ++k;
						for(; k<s.Length && char.IsDigit(s[k]); ++k) { }
						if(t2 == Type.int_type)
						{
							int n2 = int.Parse(s.Substring(from2, k - from2));
							defType = new ConstType(n2);
						}
						else
						{
							float n2 = float.Parse(s.Substring(from2, k - from2));
							defType = new ConstType(n2);
						}
					}
				}
				Decl d = @params.insertDecl(str, t2, DECL.PARAM, defType);
			}

			FuncType f = new FuncType(t, @params, false, cfunc);
			n = n.ToLowerInvariant();
			runtimeEnviron.funcDecls.insertDecl(n, f, DECL.FUNC);
		}
		return null;
	}

	private static HashSet<string> _ulibkws = new HashSet<string>();

	private static string loadUserLib(string userlib)
	{
		string t = "userlibs/" + userlib;

		string lib = "";
		StreamReader @in = new StreamReader(t);//ifstream

		bnext(@in);
		while(curr!=0)
		{
			if(curr == '.')
			{
				if(bnext(@in) != -1) return "expecting identifier after '.'";

				if(text == "lib")
				{
					if(bnext(@in) != -2) return "expecting string after lib directive";
					lib = text;
				}
				else
				{
					return "unknown decl directive";
				}
				bnext(@in);
			}
			else if(curr == -1)
			{
				if(lib.Length==0) return "function decl without lib directive";

				string id = text;
				string lower_id = id.ToLowerInvariant();

				if(_ulibkws.Contains(lower_id)) return "duplicate identifier";

				_ulibkws.Add(lower_id);

				Type ty = null;
				switch(bnext(@in))
				{
					case '%':
						ty = Type.int_type;
						break;
					case '#':
						ty = Type.float_type;
						break;
					case '$':
						ty = Type.string_type;
						break;
				}
				if(ty!=null) bnext(@in);
				else ty = Type.void_type;

				DeclSeq @params = new DeclSeq();

				if(curr != '(') return "expecting '(' after function identifier";
				bnext(@in);
				if(curr != ')')
				{
					for(; ; )
					{
						if(curr != -1) break;
						string arg = text;

						Type ty2 = null;
						switch(bnext(@in))
						{
							case '%':
								ty2 = Type.int_type;
								break;
							case '#':
								ty2 = Type.float_type;
								break;
							case '$':
								ty2 = Type.string_type;
								break;
							case '*':
								ty2 = Type.null_type;
								break;
						}
						if(ty2!=null) bnext(@in);
						else ty2 = Type.int_type;

						ConstType defType = null;

						Decl d = @params.insertDecl(arg, ty2, DECL.PARAM, defType);

						if(curr != ',') break;
						bnext(@in);
					}
				}
				if(curr != ')') return "expecting ')' after function decl";

				keyWords.Add(id);

				FuncType fn = new FuncType(ty, @params, true, true);

				runtimeEnviron.funcDecls.insertDecl(lower_id, fn, DECL.FUNC);

				if(bnext(@in) == ':')
				{
					//real name?
					bnext(@in);
					if(curr != -1 && curr != -2) return "expecting identifier or string after alias";
					id = text;
					bnext(@in);
				}

				userFuncs.Add(new UserFunc(lower_id, id, lib));
			}
		}
		return null;
	}

	private static string linkUserLibs()
	{
		_ulibkws.Clear();

		//WIN32_FIND_DATA fd;
		//HANDLE h = FindFirstFile("userlibs/*.decls", &fd);
		//if (h == INVALID_HANDLE_VALUE) return null;
		DirectoryInfo dir = new DirectoryInfo("userlibs");
		if(!dir.Exists){return null;}
		FileInfo[] files = dir.GetFiles("*.decls");
		if(files.Length==0) { return null; }

		string err = null;
		foreach(FileInfo file in files)
		{
			if(loadUserLib(file.Name) is string e)
			{
				err = $"Error in userlib '{file.Name}' - {e}";
				break;
			}
		}
		_ulibkws.Clear();

		return err;
	}
}