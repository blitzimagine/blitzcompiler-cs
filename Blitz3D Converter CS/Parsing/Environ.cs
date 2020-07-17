using System;
using System.Collections.Generic;

namespace Blitz3D.Converter.Parsing
{
	///<summary>An environ represent a stack frame block.</summary>
	public class Environ
	{
		public readonly int level;
		public readonly DeclSeq decls = new DeclSeq();

		public readonly Environ parent;
		public readonly Type returnType;

		public Environ(Type returnType, int level, Environ parent)
		{
			this.level = level;
			this.parent = parent;
			this.returnType = returnType;
		}

		public Decl FindDecl(string id)
		{
			for(Environ e = this; e!=null; e = e.parent)
			{
				if(e.decls.findDecl(id) is Decl d)
				{
					if((d.kind & (DECL.LOCAL | DECL.PARAM))==0)
					{
						return d;
					}
					else if(e == this)
					{
						return d;
					}
					
				}
			}
			return null;
		}
		
		public readonly DeclSeq funcDecls = new DeclSeq();

		public Decl FindFunc(string s)
		{
			for(Environ e = this; e!=null; e = e.parent)
			{
				if(e.funcDecls.findDecl(s) is Decl d)
				{
					return d;
				}
			}
			return null;
		}
		
		public readonly DeclSeq typeDecls = new DeclSeq();

		public Type FindType(string s)
		{
			switch(s)
			{
				case "%":return Type.Int;
				case "#":return Type.Float;
				case "$":return Type.String;
			}
			for(Environ e = this; e!=null; e = e.parent)
			{
				if(e.typeDecls.findDecl(s) is Decl d)
				{
					return d.type as StructType;
				}
			}
			return null;
		}

		
		private readonly List<Label> labels = new List<Label>();

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

		public Label DefineLabel(string name)
		{
			Label label = GetLabel(name);
			if(label.Name!=null)
			{
				throw new Exception($"Label already defined: {name}");
			}
			label.Name = name;
			return label;
		}
	}
}