using System;
using System.Collections.Generic;

namespace Blitz3D.Converter.Parsing
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

		public Decl findDecl(string id)
		{
			for(Environ e = this; e!=null; e = e.globals)
			{
				Decl d = e.decls.findDecl(id);
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
			switch(s)
			{
				case "%":return Type.Int;
				case "#":return Type.Float;
				case "$":return Type.String;
			}
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

		/// <summary>Finds label if it exists, otherwise creates one.</summary>
		public Label GetLabel(string id)
		{
			if(string.IsNullOrEmpty(id))
			{
				return Label.__DATA;
			}
			id = id.ToLowerInvariant();
			//Find existing label
			foreach(Label label in labels)
			{
				if(label.ID == id)
				{
					return label;
				}
			}
			//Create new label
			Label l = new Label(id);
			labels.Add(l);
			return l;
		}

		public Label DefineLabel(string id, string name)
		{
			Label label = GetLabel(id);
			if(label.Name!=null){throw new Exception($"Label already defined: {name}");}
			label.Name = name;
			return label;
		}
	}
}