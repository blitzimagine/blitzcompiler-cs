using System.Collections.Generic;

namespace Blitz3D.Compiling
{
	public enum DECL
	{
		FUNC=1,
		ARRAY=2,
		STRUCT=4,
		//NOT vars
		GLOBAL=8,
		LOCAL=16,
		PARAM=32,
		FIELD=64 //ARE vars
	}

	public class Decl
	{
		public readonly string name;
		public readonly Type type; //type
		public readonly DECL kind;
		public int offset;
		public readonly ConstType defType; //default value
		public Decl(string s, Type t, DECL k, ConstType d = null)
		{
			name = s;
			type = t;
			kind = k;
			defType = d;
		}

		public virtual void getName(ref string buff)
		{
			buff = name;
		}
	}

	public class DeclSeq
	{
		public readonly List<Decl> decls = new List<Decl>();

		public Decl findDecl(string s)
		{
			foreach(Decl decl in decls)
			{
				if(decl.name == s)
				{
					return decl;
				}
			}
			return null;
		}

		public Decl insertDecl(string s, Type t, DECL kind, ConstType d = null)
		{
			if(findDecl(s)!=null)
			{
				return null;
			}
			Decl n = new Decl(s, t, kind, d);
			decls.Add(n);
			return n;
		}
		public int Count => decls.Count;
	}
}