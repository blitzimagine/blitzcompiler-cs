using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace Blitz3D.Parsing
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
	/// <summary>The Toker converts an inout stream into tokens for use by the parser.</summary>
	public class Tokenizer
	{
		private static readonly Dictionary<string,Keyword> lowerTokes = new Dictionary<string, Keyword>
		{
			{"dim", Keyword.DIM},
			{"goto", Keyword.GOTO},
			{"gosub", Keyword.GOSUB},
			{"return", Keyword.RETURN},
			{"exit", Keyword.EXIT},
			{"if", Keyword.IF},
			{"then", Keyword.THEN},
			{"else", Keyword.ELSE},
			{"endif", Keyword.ENDIF},
			{"end if", Keyword.ENDIF},
			{"elseif", Keyword.ELSEIF},
			{"else if", Keyword.ELSEIF},
			{"while", Keyword.WHILE},
			{"wend", Keyword.WEND},
			{"for", Keyword.FOR},
			{"to", Keyword.TO},
			{"step", Keyword.STEP},
			{"next", Keyword.NEXT},
			{"function", Keyword.FUNCTION},
			{"end function", Keyword.ENDFUNCTION},
			{"type", Keyword.TYPE},
			{"end type", Keyword.ENDTYPE},
			{"each", Keyword.EACH},
			{"local", Keyword.LOCAL},
			{"global", Keyword.GLOBAL},
			{"field", Keyword.FIELD},
			{"const", Keyword.BBCONST},
			{"select", Keyword.SELECT},
			{"case", Keyword.CASE},
			{"default", Keyword.DEFAULT},
			{"end select", Keyword.ENDSELECT},
			{"repeat", Keyword.REPEAT},
			{"until", Keyword.UNTIL},
			{"forever", Keyword.FOREVER},
			{"data", Keyword.DATA},
			{"read", Keyword.READ},
			{"restore", Keyword.RESTORE},
			{"abs", Keyword.ABS},
			{"sgn", Keyword.SGN},
			{"mod", Keyword.MOD},
			{"pi", Keyword.PI},
			{"true", Keyword.BBTRUE},
			{"false", Keyword.BBFALSE},
			{"int", Keyword.BBINT},
			{"float", Keyword.BBFLOAT},
			{"str", Keyword.BBSTR},
			{"include", Keyword.INCLUDE},

			{"new", Keyword.BBNEW},
			{"delete", Keyword.BBDELETE},
			{"first", Keyword.FIRST},
			{"last", Keyword.LAST},
			{"insert", Keyword.INSERT},
			{"before", Keyword.BEFORE},
			{"after", Keyword.AFTER},
			{"null", Keyword.BBNULL},
			{"object", Keyword.OBJECT},
			{"handle", Keyword.BBHANDLE},

			{"and", Keyword.AND},
			{"or", Keyword.OR},
			{"xor", Keyword.XOR},
			{"not", Keyword.NOT},
			{"shl", Keyword.SHL},
			{"shr", Keyword.SHR},
			{"sar", Keyword.SAR}
		};

		private readonly StreamReader input;
		private int curr_row;

		public Tokenizer(StreamReader input)
		{
			this.input = input;
			curr_row = -1;
			Nextline();
		}

		public Point Pos => new Point(Current.from, curr_row);
		public Keyword Curr => Current.Keyword;

		public Keyword Next()
		{
			if(Current != null)
			{
				Current = Current.NextToken;
			}
			if(Current is null)
			{
				Nextline();
			}
			return Current.Keyword;
		}

		/// <summary>Assert on Curr (non-consuming)</summary>
		public void AssertCurr(Keyword keyword, Func<string,Exception> handler, string message)
		{
			if(Curr!=keyword){throw handler(message);}
		}

		/// <summary>Move to next and then Assert</summary>
		public void AssertNext(Keyword keyword, Func<string,Exception> handler, string message)
		{
			if(Next()!=keyword){throw handler(message);}
		}

		/// <summary>Assert on Curr, then move to next</summary>
		public void AssertSkip(Keyword keyword, Func<string,Exception> handler, string message)
		{
			if(Curr!=keyword){throw handler(message);}
			Next();
		}

		public bool TryTake(out Keyword keyword, params Keyword[] keywords) => TryTake(out keyword, ((ICollection<Keyword>)keywords).Contains);

		public bool TryTake(out Keyword keyword, Predicate<Keyword> condition)
		{
			keyword = Curr;
			return TrySkip(condition);
		}

		public bool TrySkip(Keyword keyword) => TrySkip(c=>c==keyword);
		public bool TrySkip(params Keyword[] keywords) => TrySkip(((ICollection<Keyword>)keywords).Contains);

		public bool TrySkip(Predicate<Keyword> condition)
		{
			if(condition(Curr))
			{
				Next();
				return true;
			}
			return false;
		}

		public void SkipWhile(Keyword keyword) => SkipWhile(c=>c==keyword);
		public void SkipWhile(params Keyword[] keywords) => SkipWhile(((ICollection<Keyword>)keywords).Contains);

		public void SkipWhileNot(Keyword keyword) => SkipWhile(c=>c!=keyword);
		public void SkipWhileNot(params Keyword[] keywords) => SkipWhile(c=>!((ICollection<Keyword>)keywords).Contains(c));

		public void SkipWhile(Predicate<Keyword> condition)
		{
			while(condition(Curr)){Next();}
		}

		public string TakeText()
		{
			string text = Current.Text;
			Next();
			return text;
		}

		public T Take<T>(Converter<string,T> converter) => converter(TakeText());

		public string Text => Current.Text;
		private Token Current;

		public Keyword LookAhead(int n)
		{
			Token current = Current;
			while(current!=null && n > 0)
			{
				current = current.NextToken;
				n--;
			}
			return current.Keyword;
		}

		private class Token
		{
			public Token NextToken = null;

			public readonly Keyword Keyword;
			public readonly string Text;

			public readonly int from;

			public Token(Keyword keyword, int from, int to, string line)
			{
				Keyword = keyword;
				Text = line.Substring(from, to - from);;
				if(Keyword == Keyword.IDENT)
				{
					Text = Utils.WrapIfCSharpKeyword(Text);
				}

				this.from = from;
			}
		}



		private void Nextline()
		{
			curr_row++;
			if(input.EndOfStream)
			{
				Current = new Token(Keyword.EOF, 0, 1, unchecked((char)-1).ToString());
				return;
			}
			Current = null;
			ref Token curr = ref Current;
			
			string line = input.ReadLine() + '\n';

			for(int k = 0; k < line.Length;)
			{
				char c = line[k];
				if(char.IsWhiteSpace(c) && c!='\n')
				{
					k++;
					continue;
				}
				else if(c == ';')
				{
					while(line[k] != '\n')
					{
						k++;
					}
					c = line[k];
				}

				int from = k;
				if(c == '\n')
				{
					curr = new Token(Keyword.NEWLINE, from, ++k, line);
				}
				else if(c == '.' && char.IsDigit(line[k + 1]))
				{
					k += 2;
					while(char.IsDigit(line[k]))
					{
						k++;
					}
					curr = new Token(Keyword.FLOATCONST, from, k, line);
				}
				else if(char.IsDigit(c))
				{
					for(++k; char.IsDigit(line[k]); ++k) { }
					if(line[k] == '.')
					{
						for(++k; char.IsDigit(line[k]); ++k) { }
						curr = new Token(Keyword.FLOATCONST, from, k, line);
					}
					else
					{
						curr = new Token(Keyword.INTCONST, from, k, line);
					}
				}
				else if(c == '%' && (line[k + 1] == '0' || line[k + 1] == '1'))
				{
					k += 2;
					while(line[k] == '0' || line[k] == '1')
					{
						k++;
					}
					curr = new Token(Keyword.BINCONST, from+1, k, line);
				}
				else if(c == '$' && IsHexDigit(line[k + 1]))
				{
					k += 2;
					while(IsHexDigit(line[k]))
					{
						k++;
					}
					curr = new Token(Keyword.HEXCONST, from+1, k, line);
				}
				else if(char.IsLetter(c))
				{
					while(char.IsLetterOrDigit(line[k]) || line[k] == '_')
					{
						k++;
					}

					string ident = line.Substring(from, k - from).ToLower();

					if(line[k] == ' ' && char.IsLetter(line[k + 1]))
					{
						int t = k + 2;
						while(char.IsLetterOrDigit(line[t]) || line[t] == '_')
						{
							t++;
						}
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
						for(int i = from; i < k; i++)
						{
							builder[i] = char.ToLower(builder[i]);
						}
						line = builder.ToString();
						curr = new Token(Keyword.IDENT, from, k, line);
					}
					else
					{
						curr = new Token(value, from, k, line);
					}
				}
				else if(c == '"')
				{
					k++;
					while(line[k] != '"' && line[k] != '\n')
					{
						k++;
					}
					if(line[k] == '"')
					{
						k++;
					}
					curr = new Token(Keyword.STRINGCONST, from, k, line);
				}
				else
				{
					int n = line[k + 1];
					if((c == '<' && n == '>') || (c == '>' && n == '<'))
					{
						curr = new Token(Keyword.NE, from, k += 2, line);
					}
					else if((c == '<' && n == '=') || (c == '=' && n == '<'))
					{
						curr = new Token(Keyword.LE, from, k += 2, line);
					}
					else if((c == '>' && n == '=') || (c == '=' && n == '>'))
					{
						curr = new Token(Keyword.GE, from, k += 2, line);
					}
					else 
					{
						curr = new Token((Keyword)c, from, ++k, line);
					}
				}
				curr = ref curr.NextToken;
			}
			if(Current == null)
			{
				throw new Exception();
			}
		}

		private static bool IsHexDigit(int c) => ('0'<=c && c<='9') || ('a'<=c && c<='f') || ('A'<=c && c<='F');
	}
}