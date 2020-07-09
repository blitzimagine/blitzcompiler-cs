using System.Collections.Generic;
using System.Text;

namespace Blitz3D
{
	public static class Utils
	{
		public static string Indent(int level) => new string('\t',level);
		public static void IAppend(this StringBuilder that, int level, string text) => that.Append(Indent(level) + text);
		public static void IAppendLine(this StringBuilder that, int level, string text) => that.AppendLine(Indent(level) + text);


		private static readonly HashSet<string> keywords = new HashSet<string>
		{
			"abstract",	"as",			"base",		"bool",	
			"break",	"byte",			"case",		"catch",	
			"char",		"checked",		"class",	"const",	
			"continue",	"decimal",		"default",	"delegate",	
			"do",		"double",		"else",		"enum",	
			"event",	"explicit",		"extern",	"false",	
			"finally",	"fixed",		"float",	"for",	
			"foreach",	"goto",			"if",		"implicit",	
			"in",		"int",			"interface","internal",	
			"is",		"lock",			"long",		"namespace",	
			"new",		"null",			"object",	"operator",	
			"out",		"override",		"params",	"private",	
			"protected","public",		"readonly",	"ref",	
			"return",	"sbyte",		"sealed",	"short",	
			"sizeof",	"stackalloc",	"static",	"string",	
			"struct",	"switch",		"this",		"throw",	
			"true",		"try",			"typeof",	"uint",	
			"ulong",	"unchecked",	"unsafe",	"ushort",	
			"using",	"virtual",		"void",		"volatile",	
			"while"
		};
		public static string WrapIfCSharpKeyword(string ident)
		{
			if(keywords.Contains(ident))
			{
				return '@'+ident;
			}
			return ident;
		}
	}
}
