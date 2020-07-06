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

	private static Dictionary<string,int> alphaTokes = new Dictionary<string, int>();
	private static Dictionary<string,int> lowerTokes = new Dictionary<string, int>();

	private static bool makeKeywords_made;
	private static void makeKeywords()
	{
		if(makeKeywords_made) return;

		alphaTokes["Dim"] = (int)DIM;
		alphaTokes["Goto"] = (int)GOTO;
		alphaTokes["Gosub"] = (int)GOSUB;
		alphaTokes["Return"] = (int)RETURN;
		alphaTokes["Exit"] = (int)EXIT;
		alphaTokes["If"] = (int)IF;
		alphaTokes["Then"] = (int)THEN;
		alphaTokes["Else"] = (int)ELSE;
		alphaTokes["EndIf"] = (int)ENDIF;
		alphaTokes["End If"] = (int)ENDIF;
		alphaTokes["ElseIf"] = (int)ELSEIF;
		alphaTokes["Else If"] = (int)ELSEIF;
		alphaTokes["While"] = (int)WHILE;
		alphaTokes["Wend"] = (int)WEND;
		alphaTokes["For"] = (int)FOR;
		alphaTokes["To"] = (int)TO;
		alphaTokes["Step"] = (int)STEP;
		alphaTokes["Next"] = (int)NEXT;
		alphaTokes["Function"] = (int)FUNCTION;
		alphaTokes["End Function"] = (int)ENDFUNCTION;
		alphaTokes["Type"] = (int)TYPE;
		alphaTokes["End Type"] = (int)ENDTYPE;
		alphaTokes["Each"] = (int)EACH;
		alphaTokes["Local"] = (int)LOCAL;
		alphaTokes["Global"] = (int)GLOBAL;
		alphaTokes["Field"] = (int)FIELD;
		alphaTokes["Const"] = (int)BBCONST;
		alphaTokes["Select"] = (int)SELECT;
		alphaTokes["Case"] = (int)CASE;
		alphaTokes["Default"] = (int)DEFAULT;
		alphaTokes["End Select"] = (int)ENDSELECT;
		alphaTokes["Repeat"] = (int)REPEAT;
		alphaTokes["Until"] = (int)UNTIL;
		alphaTokes["Forever"] = (int)FOREVER;
		alphaTokes["Data"] = (int)DATA;
		alphaTokes["Read"] = (int)READ;
		alphaTokes["Restore"] = (int)RESTORE;
		alphaTokes["Abs"] = (int)ABS;
		alphaTokes["Sgn"] = (int)SGN;
		alphaTokes["Mod"] = (int)MOD;
		alphaTokes["Pi"] = (int)PI;
		alphaTokes["True"] = (int)BBTRUE;
		alphaTokes["False"] = (int)BBFALSE;
		alphaTokes["Int"] = (int)BBINT;
		alphaTokes["Float"] = (int)BBFLOAT;
		alphaTokes["Str"] = (int)BBSTR;
		alphaTokes["Include"] = (int)INCLUDE;

		alphaTokes["New"] = (int)BBNEW;
		alphaTokes["Delete"] = (int)BBDELETE;
		alphaTokes["First"] = (int)FIRST;
		alphaTokes["Last"] = (int)LAST;
		alphaTokes["Insert"] = (int)INSERT;
		alphaTokes["Before"] = (int)BEFORE;
		alphaTokes["After"] = (int)AFTER;
		alphaTokes["Null"] = (int)BBNULL;
		alphaTokes["Object"] = (int)OBJECT;
		alphaTokes["Handle"] = (int)BBHANDLE;

		alphaTokes["And"] = (int)AND;
		alphaTokes["Or"] = (int)OR;
		alphaTokes["Xor"] = (int)XOR;
		alphaTokes["Not"] = (int)NOT;
		alphaTokes["Shl"] = (int)SHL;
		alphaTokes["Shr"] = (int)SHR;
		alphaTokes["Sar"] = (int)SAR;

		foreach(var entry in alphaTokes)
		{
			lowerTokes[entry.Key.ToLowerInvariant()] = entry.Value;
		}
		makeKeywords_made = true;
	}
	public Toker(StreamReader @in)
	{
		this.@in = @in;
		curr_row = -1;
		makeKeywords();
		nextline();
	}

	public int pos => ((curr_row) << 16) | (tokes[curr_toke].from);
	public Keyword curr => tokes[curr_toke].n;

	public Keyword next()
	{
		if(++curr_toke == tokes.Count) nextline();
		return curr;
	}
	public string text
	{
		get
		{
			int from = tokes[curr_toke].from;
			int to = tokes[curr_toke].to;
			return line.Substring(from, to - from);
		}
	}
	public Keyword lookAhead(int n) => tokes[curr_toke + n].n;

	//public static int chars_toked;

	public static Dictionary<string, int> getKeywords()
	{
		makeKeywords();
		return alphaTokes;
	}

	private class Toke
	{
		public readonly Keyword n;
		public readonly int from, to;
		public Toke(Keyword n, int f, int t, string line)
		{
			this.n = n;
			this.from = f;
			this.to = t;
			this.line = line;
		}

		private readonly string line;

		public string Text => line?.Substring(from, to - from)??null;
	};

	private StreamReader @in;
	private string line;
	private List<Toke> tokes = new List<Toke>();
	private void nextline()
	{
		++curr_row;
		curr_toke = 0;
		tokes.Clear();
		if(@in.EndOfStream)
		{
			line = unchecked((char)-1).ToString();//EOF
			tokes.Add(new Toke((Keyword)(-1)/*EOF*/, 0, 1, line));
			return;
		}

		line = @in.ReadLine();
		line += '\n';
		//chars_toked += line.Length;

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

				if(!lowerTokes.TryGetValue(ident, out int value))
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

				tokes.Add(new Toke((Keyword)value, from, k, line));
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
			Environment.Exit(0);
		}
	}
	private int curr_row, curr_toke;

	private static bool IsHexDigit(int c) => ('0'<=c && c<='9') || ('a'<=c && c<='f') || ('A'<=c && c<='F');
};