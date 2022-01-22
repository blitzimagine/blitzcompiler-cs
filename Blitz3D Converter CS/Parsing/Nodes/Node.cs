using System.Collections.Generic;
using System.Text;

namespace Blitz3D.Converter.Parsing.Nodes
{
	public abstract class Node
	{
		//used user funcs...
		public static HashSet<string> usedfuncs = new HashSet<string>();

		//helper funcs

		private static int genLabel_cnt;
		////////////////////////////////
		// Generate a fresh ASM label //
		////////////////////////////////
		public static string genLabel()
		{
			return $"_{++genLabel_cnt}";
		}

		public virtual void Semant(Environ e){}

		/////////////////////////////////
		// calculate the type of a tag //
		/////////////////////////////////
		public static Type tagType(string tag, Environ e)
		{
			if(tag.Length>0)
			{
				return e.FindType(tag);
			}
			return null;
		}
		
		public static string GetAccessors(DeclKind kind, bool constant = false, Type type = null)
		{
			if(constant && type!=null && type.IsPrimative)
			{
				return "public const ";
			}
			StringBuilder builder = new StringBuilder();
			switch(kind)
			{
				case DeclKind.Global:builder.Append("public static ");break;
				case DeclKind.Field:builder.Append("public ");break;
			}
			if(constant && builder.Length>0)
			{
				builder.Append("readonly ");
			}
			return builder.ToString();
		}
	}
}