using System;
using System.Collections.Generic;
using System.IO;

using Blitz3D.Converter.Parsing.Nodes;

namespace Blitz3D.Converter.Parsing
{
	public class Libs
	{
		//linkLibs
		private readonly List<string> keyWords = new List<string>();
		public Environ RuntimeEnviron{get;} = new Environ(Type.Int, 0, null);
		public List<UserFunc> UserFuncs{get;} = new List<UserFunc>();

		private int curr;
		private string text;

		private Libs(){}

		public static Libs InitLibs()
		{
			Libs libs = new Libs();
			libs.linkRuntime();
			libs.LinkUserLibs();
			return libs;
		}

		private int bnext(StreamReader input)//istream
		{
			text = "";

			int t;
			while(true)
			{
				while(char.IsWhiteSpace((char)input.Peek()))
				{
					input.Read();
				}
				if(input.EndOfStream)
				{
					curr = 0;
					return curr;
				}
				t = input.Read();
				if(t != ';')
				{
					break;
				}
				while(!input.EndOfStream && input.Read() != '\n'){}
			}

			if(char.IsLetter((char)t))
			{
				text += (char)t;
				while(char.IsLetterOrDigit((char)input.Peek()) || input.Peek() == '_')
				{
					text += (char)input.Read();
				}
				curr = -1;
				return curr;
			}
			if(t == '"')
			{
				while(input.Peek() != '"')
				{
					text += (char)input.Read();
				}
				input.Read();
				curr = -2;
				return curr;
			}

			curr = t;
			return curr;
		}

		private void linkRuntime()
		{
			foreach(string sym in Symbols.GetLinkSymbols())
			{
				//internal?
				if(sym[0] == '_'){continue;}

				int k = 0;

				bool cfunc = false;
				if(sym[0] == '!')
				{
					cfunc = true;
					k++;
				}

				keyWords.Add(sym.Substring(k));
				
				//global!
				Type funcType = TakeType(sym, ref k);
				string name = TakeIdentifier(sym, ref k);
				DeclSeq @params = new DeclSeq();
				while(k<sym.Length)
				{
					Type paramType = TakeType(sym, ref k);
					string str = TakeIdentifier(sym, ref k);
					ExprNode defType = null;
					if(k<sym.Length && sym[k] == '=')
					{
						int from2 = ++k;
						if(k<sym.Length && sym[k] == '"')
						{
							k++;
							while(sym[k] != '"')
							{
								k++;
							}
							defType = new StringConstNode(sym.Substring(from2, k - from2+1));
							k++;
						}
						else
						{
							if(k<sym.Length && sym[k] == '-')
							{
								k++;
							}
							while(k<sym.Length && char.IsDigit(sym,k))
							{
								k++;
							}
							if(paramType == Type.Int)
							{
								defType = new IntConstNode(sym.Substring(from2, k - from2));
							}
							else
							{
								defType = new FloatConstNode(sym.Substring(from2, k - from2));
							}
						}
					}
					@params.InsertDecl(str, paramType, DeclKind.Param, defType);
				}

				FuncType f = new FuncType(funcType, @params, cfunc);
				Decl decl = RuntimeEnviron.FuncDecls.InsertDecl(name, f, DeclKind.Func);
				decl.Name = "Blitz3D."+name;
			}
		}

		private static Type TakeType(string str, ref int index)
		{
			Type type = str[index] switch
			{
				'%' => Type.Int,
				'#' => Type.Float,
				'$' => Type.String,
				_ => null
			};
			if(type != null)
			{
				index++;
			}
			return type ?? Type.Void;
		}

		private static string TakeIdentifier(string str, ref int index)
		{
			int from = index;
			while(index<str.Length && (char.IsLetterOrDigit(str[index]) || str[index] == '_'))
			{
				index++;
			}
			return str.Substring(from, index - from);
		}

		private void LinkUserLibs()
		{
			DirectoryInfo dir = new DirectoryInfo("userlibs");
			if(!dir.Exists){return;}
			
			FileInfo[] userlibs = dir.GetFiles("*.decls");
				
			HashSet<string> _ulibkws = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach(FileInfo userlib in userlibs)
			{
				if(!TryLoadUserLib(_ulibkws, userlib, out string err))
				{
					throw new Exception($"Error in userlib '{userlib.Name}' - {err}");
				}
			}
		}

		private bool TryLoadUserLib(HashSet<string> _ulibkws, FileInfo userlib, out string err)
		{
			using StreamReader input = new StreamReader(userlib.OpenRead());
			string lib = "";

			bnext(input);
			while(curr!=0)
			{
				if(curr == '.')
				{
					if(bnext(input) != -1)
					{
						err = "expecting identifier after '.'";
						return false;
					}

					if(text == "lib")
					{
						if(bnext(input) != -2)
						{
							err = "expecting string after lib directive";
							return false;
						}
						lib = text;
					}
					else
					{
						err = "unknown decl directive";
						return false;
					}
					bnext(input);
				}
				else if(curr == -1)
				{
					if(lib.Length==0)
					{
						err = "function decl without lib directive";
						return false;
					}

					string id = text;

					if(!_ulibkws.Add(id))
					{
						err = "duplicate identifier";
						return false;
					}

					Type ty = bnext(input) switch
					{
						'%' => Type.Int,
						'#' => Type.Float,
						'$' => Type.String,
						_ => Type.Void,
					};
					if(ty != Type.Void)
					{
						bnext(input);
					}

					DeclSeq @params = new DeclSeq();

					if(curr != '(')
					{
						err = "expecting '(' after function identifier";
						return false;
					}
					bnext(input);
					if(curr != ')')
					{
						while(true)
						{
							if(curr != -1) break;
							string arg = text;

							Type ty2 = null;
							switch(bnext(input))
							{
								case '%':ty2 = Type.Int;break;
								case '#':ty2 = Type.Float;break;
								case '$':ty2 = Type.String;break;
								case '*':ty2 = Type.Null;break;
							}
							if(ty2!=null)
							{
								bnext(input);
							}
							else
							{
								ty2 = Type.Int;
							}

							@params.InsertDecl(arg, ty2, DeclKind.Param);

							if(curr != ',') break;
							bnext(input);
						}
					}
					if(curr != ')')
					{
						err = "expecting ')' after function decl";
						return false;
					}

					keyWords.Add(id);

					FuncType fn = new FuncType(ty, @params, true);

					RuntimeEnviron.FuncDecls.InsertDecl(id, fn, DeclKind.Func);

					if(bnext(input) == ':')
					{
						//real name?
						bnext(input);
						if(curr != -1 && curr != -2)
						{
							err = "expecting identifier or string after alias";
							return false;
						}
						id = text;
						bnext(input);
					}

					//userFuncs.Add(new UserFunc(id.ToLowerInvariant(), id, lib));
				}
			}
			err = null;
			return true;
		}
	}
}