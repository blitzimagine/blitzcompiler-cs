using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Blitz3D.Converter.Parsing
{
	public enum TokenType:int
	{
		/// <summary>End Of File</summary>
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

		LE = 0x1000,
		GE,
		NE,
		IDENT,
		INTCONST,
		BINCONST,
		HEXCONST,
		FLOATCONST,
		STRINGCONST,


		#region Keywords
		KeywordsStart = 0x8000,

		DIM,
		GOTO,
		GOSUB,
		EXIT,
		RETURN,
		IF,
		THEN,
		ELSE,
		ENDIF,
		END_IF = ENDIF,
		ELSEIF,
		ELSE_IF = ELSEIF,
		WHILE,
		WEND,
		FOR,
		TO,
		STEP,
		NEXT,
		FUNCTION,
		END_FUNCTION,
		TYPE,
		END_TYPE,
		EACH,
		GLOBAL,
		LOCAL,
		FIELD,
		CONST,
		SELECT,
		CASE,
		DEFAULT,
		END_SELECT,
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
		TRUE,
		FALSE,
		INT,
		FLOAT,
		STR,
		INCLUDE,

		NEW,
		DELETE,
		FIRST,
		LAST,
		INSERT,
		BEFORE,
		AFTER,
		NULL,
		OBJECT,
		HANDLE,

		AND,
		OR,
		XOR,
		NOT,
		SHL,
		SHR,
		SAR
		#endregion
	}

	public class Token
	{
		public Token NextToken = null;

		public readonly TokenType Type;

		public readonly string Text;

		public readonly int from;

		public Token(TokenType keyword, string text)
		{
			Type = keyword;
			Text = text;
			if(Type == TokenType.IDENT)
			{
				Text = Utils.WrapIfCSharpKeyword(Text);
			}
		}

		public Token(TokenType keyword, int from, int to, string line):this(keyword,line.Substring(from, to - from))
		{
			this.from = from;
		}
	}

	/// <summary>The Toker converts an inout stream into tokens for use by the parser.</summary>
	public class Tokenizer
	{
		private readonly IReadOnlyDictionary<string,TokenType> lowerTokes = GetKeywordDictionary();

		public static IReadOnlyDictionary<string,TokenType> GetKeywordDictionary()
		{
			Dictionary<string, TokenType> keywords = new Dictionary<string, TokenType>(StringComparer.OrdinalIgnoreCase);
			foreach(string name in Enum.GetNames(typeof(TokenType)))
			{
				TokenType type = Enum.Parse<TokenType>(name);
				if(type>TokenType.KeywordsStart)
				{
					keywords.Add(name.Replace('_', ' '), type);
				}
			}
			return keywords;
		}

		private readonly StreamReader input;
		private int curr_row;

		public Tokenizer(StreamReader input)
		{
			this.input = input;
			curr_row = -1;
			Nextline();
		}

		public Point Pos => new Point(Current.from, curr_row);

		private Token Current;

		public TokenType CurrType => Current.Type;

		public TokenType NextType()
		{
			if(Current != null)
			{
				Current = Current.NextToken;
			}
			if(Current is null)
			{
				Nextline();
			}
			return Current.Type;
		}

		/// <summary>Assert on Curr (non-consuming)</summary>
		public void AssertCurr(TokenType keyword, Func<string,Exception> handler, string message)
		{
			if(CurrType!=keyword)
			{
				throw handler(message);
			}
		}

		/// <summary>Assert on Curr, then move to next</summary>
		public void AssertSkip(TokenType keyword, Func<string,Exception> handler, string message)
		{
			if(CurrType!=keyword)
			{
				throw handler(message);
			}
			NextType();
		}

		public bool TryTake(out TokenType keyword, params TokenType[] keywords)
		{
			keyword = CurrType;
			return TrySkip(((ICollection<TokenType>)keywords).Contains);
		}

		public bool TrySkip(TokenType keyword) => TrySkip(c=>c==keyword);
		//public bool TrySkip(params Keyword[] keywords) => TrySkip(((ICollection<Keyword>)keywords).Contains);

		private bool TrySkip(Predicate<TokenType> condition)
		{
			if(condition(CurrType))
			{
				NextType();
				return true;
			}
			return false;
		}

		public void SkipWhile(TokenType keyword) => SkipWhile(c=>c==keyword);
		public void SkipWhile(params TokenType[] keywords) => SkipWhile(((ICollection<TokenType>)keywords).Contains);

		public void SkipWhileNot(TokenType keyword) => SkipWhile(c=>c!=keyword);
		public void SkipWhileNot(params TokenType[] keywords) => SkipWhile(c=>!((ICollection<TokenType>)keywords).Contains(c));

		public void SkipWhile(Predicate<TokenType> condition)
		{
			while(condition(CurrType)){NextType();}
		}

		public Token Take()
		{
			Token token = Current;
			NextType();
			return token;
		}

		public TokenType TakeType() => Take().Type;

		public string TakeText() => Take().Text;

		public TokenType LookAhead(int n)
		{
			Token current = Current;
			while(current!=null && n > 0)
			{
				current = current.NextToken;
				n--;
			}
			return current.Type;
		}

		private void Nextline()
		{
			curr_row++;
			if(input.EndOfStream)
			{
				Current = new Token(TokenType.EOF, null);
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
					curr = new Token(TokenType.NEWLINE, from, ++k, line);
				}
				else if(c == '.' && char.IsDigit(line[k + 1]))
				{
					k += 2;
					while(char.IsDigit(line[k]))
					{
						k++;
					}
					curr = new Token(TokenType.FLOATCONST, from, k, line);
				}
				else if(char.IsDigit(c))
				{
					k++;
					while(char.IsDigit(line[k]))
					{
						k++;
					}
					if(line[k] == '.')
					{
						k++;
						while(char.IsDigit(line[k]))
						{
							k++;
						}
						curr = new Token(TokenType.FLOATCONST, from, k, line);
					}
					else
					{
						curr = new Token(TokenType.INTCONST, from, k, line);
					}
				}
				else if(c == '%' && (line[k + 1] == '0' || line[k + 1] == '1'))
				{
					k += 2;
					while(line[k] == '0' || line[k] == '1')
					{
						k++;
					}
					curr = new Token(TokenType.BINCONST, "0b"+line.Substring(from+1, k-from-1));
				}
				else if(c == '$' && IsHexDigit(line[k + 1]))
				{
					k += 2;
					while(IsHexDigit(line[k]))
					{
						k++;
					}
					curr = new Token(TokenType.HEXCONST, "0x"+line.Substring(from+1, k-from-1));
				}
				else if(char.IsLetter(c))
				{
					while(char.IsLetterOrDigit(line[k]) || line[k] == '_')
					{
						k++;
					}

					string ident = line.Substring(from, k - from);

					if(line[k] == ' ' && char.IsLetter(line[k + 1]))
					{
						int t = k + 2;
						while(char.IsLetterOrDigit(line[t]) || line[t] == '_')
						{
							t++;
						}
						string s = line.Substring(from, t - from);
						if(lowerTokes.ContainsKey(s))
						{
							k = t;
							ident = s;
						}
					}

					if(lowerTokes.TryGetValue(ident, out TokenType value))
					{
						curr = new Token(value, from, k, line);
					}
					else
					{
						curr = new Token(TokenType.IDENT, from, k, line);
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
					curr = new Token(TokenType.STRINGCONST, from, k, line);
				}
				else
				{
					int n = line[k + 1];
					if((c == '<' && n == '>') || (c == '>' && n == '<'))
					{
						curr = new Token(TokenType.NE, from, k += 2, line);
					}
					else if((c == '<' && n == '=') || (c == '=' && n == '<'))
					{
						curr = new Token(TokenType.LE, from, k += 2, line);
					}
					else if((c == '>' && n == '=') || (c == '=' && n == '>'))
					{
						curr = new Token(TokenType.GE, from, k += 2, line);
					}
					else 
					{
						curr = new Token((TokenType)c, from, ++k, line);
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