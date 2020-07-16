using System.Collections.Generic;
using Blitz3D.Converter.Parsing.Nodes;

namespace Blitz3D.Converter.Parsing
{
	public enum DECL
	{
		//NOT vars
		FUNC	= 1<<0,
		ARRAY	= 1<<1,
		STRUCT	= 1<<2,
		
		//ARE vars
		GLOBAL	= 1<<3,
		LOCAL	= 1<<4,
		PARAM	= 1<<5,
		FIELD	= 1<<6,
	}

	public class Decl:Identifier
	{
		public readonly Type type; //type
		public readonly DECL kind;
		public readonly ExprNode defType; //ConstType //default value
		public Decl(string name, Type t, DECL k, ExprNode d = null):base(name)
		{
			Name = name;
			type = t;
			kind = k;
			defType = d;
		}
	}

	public class DeclSeq
	{
		public readonly List<Decl> decls = new List<Decl>();

		public Decl findDecl(string id)
		{
			id = id.ToLowerInvariant();
			foreach(Decl decl in decls)
			{
				if(decl.ID == id)
				{
					return decl;
				}
			}
			return null;
		}

		public Decl insertDecl(string name, Type type, DECL kind, ExprNode defType = null)
		{
			if(findDecl(name)!=null){return null;}

			Decl n = new Decl(name, type, kind, defType);
			decls.Add(n);
			return n;
		}
		public int Count => decls.Count;
	}
}