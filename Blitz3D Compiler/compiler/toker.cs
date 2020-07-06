/*

  The Toker converts an inout stream into tokens for use by the parser.

  */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static Keyword;

public enum Keyword:int
{
	//Not actually a keyword, this is end of file
	EOF = -1,

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
};

public class Toker
{
	//private int chars_toked;

	private static Dictionary<string,Keyword> alphaTokes = new Dictionary<string, Keyword>();
	private static Dictionary<string,Keyword> lowerTokes = new Dictionary<string, Keyword>();

	private static bool makeKeywords_made;
	private static void makeKeywords()
	{
		if(makeKeywords_made) return;

		alphaTokes["Dim"] = DIM;
		alphaTokes["Goto"] = GOTO;
		alphaTokes["Gosub"] = GOSUB;
		alphaTokes["Return"] = RETURN;
		alphaTokes["Exit"] = EXIT;
		alphaTokes["If"] = IF;
		alphaTokes["Then"] = THEN;
		alphaTokes["Else"] = ELSE;
		alphaTokes["EndIf"] = ENDIF;
		alphaTokes["End If"] = ENDIF;
		alphaTokes["ElseIf"] = ELSEIF;
		alphaTokes["Else If"] = ELSEIF;
		alphaTokes["While"] = WHILE;
		alphaTokes["Wend"] = WEND;
		alphaTokes["For"] = FOR;
		alphaTokes["To"] = TO;
		alphaTokes["Step"] = STEP;
		alphaTokes["Next"] = NEXT;
		alphaTokes["Function"] = FUNCTION;
		alphaTokes["End Function"] = ENDFUNCTION;
		alphaTokes["Type"] = TYPE;
		alphaTokes["End Type"] = ENDTYPE;
		alphaTokes["Each"] = EACH;
		alphaTokes["Local"] = LOCAL;
		alphaTokes["Global"] = GLOBAL;
		alphaTokes["Field"] = FIELD;
		alphaTokes["Const"] = BBCONST;
		alphaTokes["Select"] = SELECT;
		alphaTokes["Case"] = CASE;
		alphaTokes["Default"] = DEFAULT;
		alphaTokes["End Select"] = ENDSELECT;
		alphaTokes["Repeat"] = REPEAT;
		alphaTokes["Until"] = UNTIL;
		alphaTokes["Forever"] = FOREVER;
		alphaTokes["Data"] = DATA;
		alphaTokes["Read"] = READ;
		alphaTokes["Restore"] = RESTORE;
		alphaTokes["Abs"] = ABS;
		alphaTokes["Sgn"] = SGN;
		alphaTokes["Mod"] = MOD;
		alphaTokes["Pi"] = PI;
		alphaTokes["True"] = BBTRUE;
		alphaTokes["False"] = BBFALSE;
		alphaTokes["Int"] = BBINT;
		alphaTokes["Float"] = BBFLOAT;
		alphaTokes["Str"] = BBSTR;
		alphaTokes["Include"] = INCLUDE;

		alphaTokes["New"] = BBNEW;
		alphaTokes["Delete"] = BBDELETE;
		alphaTokes["First"] = FIRST;
		alphaTokes["Last"] = LAST;
		alphaTokes["Insert"] = INSERT;
		alphaTokes["Before"] = BEFORE;
		alphaTokes["After"] = AFTER;
		alphaTokes["Null"] = BBNULL;
		alphaTokes["Object"] = OBJECT;
		alphaTokes["Handle"] = BBHANDLE;

		alphaTokes["And"] = AND;
		alphaTokes["Or"] = OR;
		alphaTokes["Xor"] = XOR;
		alphaTokes["Not"] = NOT;
		alphaTokes["Shl"] = SHL;
		alphaTokes["Shr"] = SHR;
		alphaTokes["Sar"] = SAR;

		foreach(var entry in alphaTokes)
		{
			lowerTokes[entry.Key.ToLowerInvariant()] = entry.Value;
		}
		makeKeywords_made = true;
	}
	public Toker(StreamReader input)
	{
		this.input = input;
		curr_row = -1;
		makeKeywords();
		nextline();
	}

	public int Pos => ((curr_row) << 16) | (tokes[curr_toke].from);
	public Keyword curr => tokes[curr_toke].Keyword;

	public Keyword next()
	{
		if(++curr_toke == tokes.Count) nextline();
		return curr;
	}
	public string text => tokes[curr_toke].Text;

	public Keyword lookAhead(int n) => tokes[curr_toke + n].Keyword;

	public static Dictionary<string, Keyword> getKeywords()
	{
		makeKeywords();
		return alphaTokes;
	}

	private class Toke
	{
		public readonly Keyword Keyword;
		public readonly int from, to;
		public Toke(Keyword n, int f, int t, string line)
		{
			this.Keyword = n;
			this.from = f;
			this.to = t;
			this.line = line;
		}

		private readonly string line;

		public string Text => line.Substring(from, to - from);
	};

	private StreamReader input;
	private string line;
	private List<Toke> tokes = new List<Toke>();
	private int curr_row, curr_toke;

	private void nextline()
	{
		curr_row++;
		curr_toke = 0;
		tokes.Clear();
		if(input.EndOfStream)
		{
			line = unchecked((char)-1).ToString();//EOF
			tokes.Add(new Toke((Keyword)(-1)/*EOF*/, 0, 1, line));
			return;
		}

		line = input.ReadLine();
		line += '\n';

		for(int k = 0; k < line.Length;)
		{
			char c = line[k];
			int from = k;
			if(c == '\n')
			{
				tokes.Add(new Toke((Keyword)c, from, ++k, line));
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
				tokes.Add(new Toke(FLOATCONST, from, k, line));
				continue;
			}
			if(char.IsDigit(c))
			{
				for(++k; char.IsDigit(line[k]); ++k) { }
				if(line[k] == '.')
				{
					for(++k; char.IsDigit(line[k]); ++k) { }
					tokes.Add(new Toke(FLOATCONST, from, k, line));
					continue;
				}
				tokes.Add(new Toke(INTCONST, from, k, line));
				continue;
			}
			if(c == '%' && (line[k + 1] == '0' || line[k + 1] == '1'))
			{
				for(k += 2; line[k] == '0' || line[k] == '1'; ++k) { }
				tokes.Add(new Toke(BINCONST, from, k, line));
				continue;
			}
			if(c == '$' && IsHexDigit(line[k + 1]))
			{
				for(k += 2; IsHexDigit(line[k]); ++k) { }
				tokes.Add(new Toke(HEXCONST, from, k, line));
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
					tokes.Add(new Toke(IDENT, from, k, line));
					continue;
				}

				tokes.Add(new Toke(value, from, k, line));
				continue;
			}
			if(c == '\"')
			{
				for(++k; line[k] != '\"' && line[k] != '\n'; ++k) { }
				if(line[k] == '\"') ++k;
				tokes.Add(new Toke(STRINGCONST, from, k, line));
				continue;
			}
			int n = line[k + 1];
			if((c == '<' && n == '>') || (c == '>' && n == '<'))
			{
				tokes.Add(new Toke(NE, from, k += 2, line));
				continue;
			}
			if((c == '<' && n == '=') || (c == '=' && n == '<'))
			{
				tokes.Add(new Toke(LE, from, k += 2, line));
				continue;
			}
			if((c == '>' && n == '=') || (c == '=' && n == '>'))
			{
				tokes.Add(new Toke(GE, from, k += 2, line));
				continue;
			}
			tokes.Add(new Toke((Keyword)c, from, ++k, line));
		}
		if(tokes.Count==0)
		{
			throw new Exception();
		}
	}

	private static bool IsHexDigit(int c) => ('0'<=c && c<='9') || ('a'<=c && c<='f') || ('A'<=c && c<='F');
};