using System.Collections.Generic;
using System.Drawing;

namespace Blitz3D.Parsing
{
	///<summary>An environ represent a stack frame block.</summary>
	public class Environ
	{
		public int level;
		public DeclSeq decls = new DeclSeq();
		public DeclSeq funcDecls = new DeclSeq();
		public DeclSeq typeDecls = new DeclSeq();

		public List<Type> types = new List<Type>();

		public List<Label> labels = new List<Label>();
		public Environ globals;
		public Type returnType;
		public string funcLabel, breakLabel;

		public Environ(string f, Type r, int l, Environ gs)
		{
			level = l;
			globals = gs;
			returnType = r;
			funcLabel = f;
		}

		public Decl findDecl(string s)
		{
			for(Environ e = this; e!=null; e = e.globals)
			{
				Decl d = e.decls.findDecl(s);
				if(d!=null)
				{
					if((d.kind & (DECL.LOCAL | DECL.PARAM))!=0)
					{
						if(e == this) return d;
					}
					else return d;
				}
			}
			return null;
		}
		public Decl findFunc(string s)
		{
			for(Environ e = this; e!=null; e = e.globals)
			{
				Decl d = e.funcDecls.findDecl(s);
				if(d!=null)
				{
					return d;
				}
			}
			return null;
		}
		public Type findType(string s)
		{
			if(s == "%") return Type.int_type;
			if(s == "#") return Type.float_type;
			if(s == "$") return Type.string_type;
			for(Environ e = this; e!=null; e = e.globals)
			{
				Decl d = e.typeDecls.findDecl(s);
				if(d!=null)
				{
					return d.type as StructType;
				}
			}
			return null;
		}
		public Label findLabel(string s)
		{
			for(int k = 0; k < labels.Count; ++k)
			{
				if(labels[k].name == s)
				{
					return labels[k];
				}
			}
			return null;
		}
		public Label insertLabel(string s, Point? def, Point? src, int sz)
		{
			Label l = new Label(s, def, src, sz);
			labels.Add(l);
			return l;
		}

		public string setBreak(string s)
		{
			string t = breakLabel;
			breakLabel = s;
			return t;
		}
	}
}