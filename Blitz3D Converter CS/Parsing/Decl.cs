using System.Collections;
using System.Collections.Generic;
using Blitz3D.Converter.Parsing.Nodes;

namespace Blitz3D.Converter.Parsing
{
	public enum DeclKind
	{
		//NOT vars
		Func	= 1<<0,
		Array	= 1<<1,
		Struct	= 1<<2,
		
		//ARE vars
		Global	= 1<<3,
		Local	= 1<<4,
		Param	= 1<<5,
		Field	= 1<<6,
	}

	public class Decl:Identifier
	{
		public Type Type{get;} //type
		public DeclKind Kind{get;}
		public ExprNode DefType{get;} //ConstType //default value

		public Decl(string name, Type t, DeclKind k, ExprNode d = null):base(name)
		{
			Name = name;
			Type = t;
			Kind = k;
			DefType = d;
		}
	}

	public class DeclSeq:IReadOnlyList<Decl>
	{
		private readonly List<Decl> decls = new List<Decl>();

		public int Count => decls.Count;

		public Decl this[int index] => decls[index];

		public Decl FindDecl(string id)
		{
			id = id.ToLowerInvariant();
			return decls.Find(decl => decl.ID == id);
		}

		public Decl InsertDecl(string name, Type type, DeclKind kind, ExprNode defType = null)
		{
			if(FindDecl(name)!=null){return null;}

			Decl n = new Decl(name, type, kind, defType);
			decls.Add(n);
			return n;
		}

		public Decl AssertNewDecl(string name, Type type, DeclKind kind, ExprNode defType = null) => InsertDecl(name, type, kind, defType) ?? throw new Ex("Duplicate identifier");
		
		public IEnumerator<Decl> GetEnumerator() => decls.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}