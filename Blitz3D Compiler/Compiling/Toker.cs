/*

  The Toker converts an inout stream into tokens for use by the parser.

  */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Blitz3D.Compiling
{
	public enum Keyword:int
	{
		//Not actually a keyword, this is end of file
		EOF = -1,

		EQ = '=',
		LT = '<',
		GT = '>',
		COMMA = ',',
		NEWLINE = '\n',

		POSITIVE = '+',
		NEGATIVE = '-',
		ADD = '+',
		SUB = '-',
		MUL = '*',
		DIV = '/',
		POW = '^',

		BITNOT = '~',

		ParenOpen = '(',
		ParenClose = ')',

		BracketOpen = '[',
		BracketClose = ']',

		Backslash = '\\',

		DIM=0x8000,
		GOTO,
		GOSUB,
		EXIT,
		RETURN,
		IF,
		THEN,
		ELSE,
		ENDIF,
		ELSEIF,
		WHILE,
		WEND,
		FOR,
		TO,
		STEP,
		NEXT,
		FUNCTION,
		ENDFUNCTION,
		TYPE,
		ENDTYPE,
		EACH,
		GLOBAL,
		LOCAL,
		FIELD,
		BBCONST,
		SELECT,
		CASE,
		DEFAULT,
		ENDSELECT,
		REPEAT,
		UNTIL,
		FOREVER,
		DATA,
		READ,
		RESTORE,
		ABS,
		SGN,
		MOD,
		PI,
		BBTRUE,
		BBFALSE,
		BBINT,
		BBFLOAT,
		BBSTR,
		INCLUDE,

		BBNEW,
		BBDELETE,
		FIRST,
		LAST,
		INSERT,
		BEFORE,
		AFTER,
		BBNULL,
		OBJECT,
		BBHANDLE,

		AND,
		OR,
		XOR,
		NOT,
		SHL,
		SHR,
		SAR,

		LE,
		GE,
		NE,
		IDENT,
		INTCONST,
		BINCONST,
		HEXCONST,
		FLOATCONST,
		STRINGCONST
	}

	public class Toker
	{
		//private int chars_toked;

		private static Dictionary<string,Keyword> alphaTokes = new Dictionary<string, Keyword>();
		private static Dictionary<string,Keyword> lowerTokes = new Dictionary<string, Keyword>();

		private static bool makeKeywords_made;
		private static void makeKeywords()
		{
			if(makeKeywords_made) return;

			alphaTokes["Dim"] = Keyword.DIM;
			alphaTokes["Goto"] = Keyword.GOTO;
			alphaTokes["Gosub"] = Keyword.GOSUB;
			alphaTokes["Return"] = Keyword.RETURN;
			alphaTokes["Exit"] = Keyword.EXIT;
			alphaTokes["If"] = Keyword.IF;
			alphaTokes["Then"] = Keyword.THEN;
			alphaTokes["Else"] = Keyword.ELSE;
			alphaTokes["EndIf"] = Keyword.ENDIF;
			alphaTokes["End If"] = Keyword.ENDIF;
			alphaTokes["ElseIf"] = Keyword.ELSEIF;
			alphaTokes["Else If"] = Keyword.ELSEIF;
			alphaTokes["While"] = Keyword.WHILE;
			alphaTokes["Wend"] = Keyword.WEND;
			alphaTokes["For"] = Keyword.FOR;
			alphaTokes["To"] = Keyword.TO;
			alphaTokes["Step"] = Keyword.STEP;
			alphaTokes["Next"] = Keyword.NEXT;
			alphaTokes["Function"] = Keyword.FUNCTION;
			alphaTokes["End Function"] = Keyword.ENDFUNCTION;
			alphaTokes["Type"] = Keyword.TYPE;
			alphaTokes["End Type"] = Keyword.ENDTYPE;
			alphaTokes["Each"] = Keyword.EACH;
			alphaTokes["Local"] = Keyword.LOCAL;
			alphaTokes["Global"] = Keyword.GLOBAL;
			alphaTokes["Field"] = Keyword.FIELD;
			alphaTokes["Const"] = Keyword.BBCONST;
			alphaTokes["Select"] = Keyword.SELECT;
			alphaTokes["Case"] = Keyword.CASE;
			alphaTokes["Default"] = Keyword.DEFAULT;
			alphaTokes["End Select"] = Keyword.ENDSELECT;
			alphaTokes["Repeat"] = Keyword.REPEAT;
			alphaTokes["Until"] = Keyword.UNTIL;
			alphaTokes["Forever"] = Keyword.FOREVER;
			alphaTokes["Data"] = Keyword.DATA;
			alphaTokes["Read"] = Keyword.READ;
			alphaTokes["Restore"] = Keyword.RESTORE;
			alphaTokes["Abs"] = Keyword.ABS;
			alphaTokes["Sgn"] = Keyword.SGN;
			alphaTokes["Mod"] = Keyword.MOD;
			alphaTokes["Pi"] = Keyword.PI;
			alphaTokes["True"] = Keyword.BBTRUE;
			alphaTokes["False"] = Keyword.BBFALSE;
			alphaTokes["Int"] = Keyword.BBINT;
			alphaTokes["Float"] = Keyword.BBFLOAT;
			alphaTokes["Str"] = Keyword.BBSTR;
			alphaTokes["Include"] = Keyword.INCLUDE;

			alphaTokes["New"] = Keyword.BBNEW;
			alphaTokes["Delete"] = Keyword.BBDELETE;
			alphaTokes["First"] = Keyword.FIRST;
			alphaTokes["Last"] = Keyword.LAST;
			alphaTokes["Insert"] = Keyword.INSERT;
			alphaTokes["Before"] = Keyword.BEFORE;
			alphaTokes["After"] = Keyword.AFTER;
			alphaTokes["Null"] = Keyword.BBNULL;
			alphaTokes["Object"] = Keyword.OBJECT;
			alphaTokes["Handle"] = Keyword.BBHANDLE;

			alphaTokes["And"] = Keyword.AND;
			alphaTokes["Or"] = Keyword.OR;
			alphaTokes["Xor"] = Keyword.XOR;
			alphaTokes["Not"] = Keyword.NOT;
			alphaTokes["Shl"] = Keyword.SHL;
			alphaTokes["Shr"] = Keyword.SHR;
			alphaTokes["Sar"] = Keyword.SAR;

			foreach(var entry in alphaTokes)
			{
				lowerTokes[entry.Key.ToLowerInvariant()] = entry.Value;
			}
			makeKeywords_made = true;
		}

		private readonly StreamReader input;
		private readonly LinkedList<Toke> tokes = new LinkedList<Toke>();
		private int curr_row;

		public Toker(StreamReader input)
		{
			this.input = input;
			curr_row = -1;
			makeKeywords();
			nextline();
		}

		public int Pos => (curr_row << 16) | (Curr.from);
		public Keyword curr => Curr.Keyword;

		public Keyword next()
		{
			if(tokes.Count>0)
			{
				tokes.RemoveFirst();
			}
			if(tokes.Count==0)
			{
				nextline();
			}
			return Curr.Keyword;
		}

		public string Text => Curr.Text;
		public Toke Curr => tokes.First.Value;

		public Keyword LookAhead(int n)
		{
			LinkedListNode<Toke> node = tokes.First;
			while(node!=null && n > 0)
			{
				node = node.Next;
				n--;
			}
			return node.Value.Keyword;
		}

		public static Dictionary<string, Keyword> getKeywords()
		{
			makeKeywords();
			return alphaTokes;
		}

		public class Toke
		{
			private readonly string line;

			public readonly Keyword Keyword;
			public readonly int from, to;

			public Toke(Keyword n, int f, int t, string line)
			{
				this.line = line;
				this.Keyword = n;
				this.from = f;
				this.to = t;
			}

			public string Text => line.Substring(from, to - from);
		}



		private void nextline()
		{
			curr_row++;
			//curr_toke = 0;
			tokes.Clear();
			if(input.EndOfStream)
			{
				tokes.AddLast(new Toke(Keyword.EOF, 0, 1, unchecked((char)-1).ToString()));
				return;
			}

			string line = input.ReadLine();
			line += '\n';

			for(int k = 0; k < line.Length;)
			{
				char c = line[k];
				int from = k;
				if(c == '\n')
				{
					tokes.AddLast(new Toke((Keyword)c, from, ++k, line));
					continue;
				}
				if(char.IsWhiteSpace(c))
				{
					++k;
					continue;
				}
				if(c == ';')
				{
					for(++k; line[k] != '\n'; ++k) { }
					continue;
				}
				if(c == '.' && char.IsDigit(line[k + 1]))
				{
					for(k += 2; char.IsDigit(line[k]); ++k) { }
					tokes.AddLast(new Toke(Keyword.FLOATCONST, from, k, line));
					continue;
				}
				if(char.IsDigit(c))
				{
					for(++k; char.IsDigit(line[k]); ++k) { }
					if(line[k] == '.')
					{
						for(++k; char.IsDigit(line[k]); ++k) { }
						tokes.AddLast(new Toke(Keyword.FLOATCONST, from, k, line));
						continue;
					}
					tokes.AddLast(new Toke(Keyword.INTCONST, from, k, line));
					continue;
				}
				if(c == '%' && (line[k + 1] == '0' || line[k + 1] == '1'))
				{
					for(k += 2; line[k] == '0' || line[k] == '1'; ++k) { }
					tokes.AddLast(new Toke(Keyword.BINCONST, from, k, line));
					continue;
				}
				if(c == '$' && IsHexDigit(line[k + 1]))
				{
					for(k += 2; IsHexDigit(line[k]); ++k) { }
					tokes.AddLast(new Toke(Keyword.HEXCONST, from, k, line));
					continue;
				}
				if(char.IsLetter(c))
				{
					for(++k; char.IsLetterOrDigit(line[k]) || line[k] == '_'; ++k) { }

					string ident = line.Substring(from, k - from).ToLower();

					if(line[k] == ' ' && char.IsLetter(line[k + 1]))
					{
						int t = k;
						for(t += 2; char.IsLetterOrDigit(line[t]) || line[t] == '_'; ++t) { }
						string s = line.Substring(from, t - from).ToLower();
						if(lowerTokes.ContainsKey(s))
						{
							k = t;
							ident = s;
						}
					}

					if(!lowerTokes.TryGetValue(ident, out Keyword value))
					{
						StringBuilder builder = new StringBuilder(line);
						for(int i = from; i < k; ++i)
						{
							builder[i] = char.ToLower(builder[i]);
						}
						line = builder.ToString();
						tokes.AddLast(new Toke(Keyword.IDENT, from, k, line));
						continue;
					}

					tokes.AddLast(new Toke(value, from, k, line));
					continue;
				}
				if(c == '\"')
				{
					for(++k; line[k] != '\"' && line[k] != '\n'; ++k) { }
					if(line[k] == '\"') ++k;
					tokes.AddLast(new Toke(Keyword.STRINGCONST, from, k, line));
					continue;
				}
				int n = line[k + 1];
				if((c == '<' && n == '>') || (c == '>' && n == '<'))
				{
					tokes.AddLast(new Toke(Keyword.NE, from, k += 2, line));
					continue;
				}
				if((c == '<' && n == '=') || (c == '=' && n == '<'))
				{
					tokes.AddLast(new Toke(Keyword.LE, from, k += 2, line));
					continue;
				}
				if((c == '>' && n == '=') || (c == '=' && n == '>'))
				{
					tokes.AddLast(new Toke(Keyword.GE, from, k += 2, line));
					continue;
				}
				tokes.AddLast(new Toke((Keyword)c, from, ++k, line));
			}
			if(tokes.Count==0)
			{
				throw new Exception();
			}
		}

		private static bool IsHexDigit(int c) => ('0'<=c && c<='9') || ('a'<=c && c<='f') || ('A'<=c && c<='F');
	}
}