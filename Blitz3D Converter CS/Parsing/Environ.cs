using System.Collections.Generic;
using System.Drawing;

namespace Blitz3D.Parsing
{
	///<summary>An environ represent a stack frame block.</summary>
	public class Environ
	{
		public readonly int level;
		public readonly DeclSeq decls = new DeclSeq();
		public readonly DeclSeq funcDecls = new DeclSeq();
		public readonly DeclSeq typeDecls = new DeclSeq();

		public readonly List<Type> types = new List<Type>();

		private readonly List<Label> labels = new List<Label>();
		
		public readonly Environ globals;
		public readonly Type returnType;
		public readonly string funcLabel;

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
			if(s == "%") return Type.Int;
			if(s == "#") return Type.Float;
			if(s == "$") return Type.String;
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


		//TODO: Make this TryAddLabel?
		public Label findLabel(string name)
		{
			for(int k = 0; k < labels.Count; ++k)
			{
				if(labels[k].name == name)
				{
					return labels[k];
				}
			}
			return null;
		}
		public Label insertLabel(string name, Point? def, Point? src)
		{
			Label l = new Label(name, def, src);
			labels.Add(l);
			return l;
		}
	}
}