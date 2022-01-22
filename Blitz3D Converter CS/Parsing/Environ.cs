using System;
using System.Collections;
using System.Collections.Generic;

namespace Blitz3D.Converter.Parsing
{
	///<summary>An environ represent a stack frame block.</summary>
	public class Environ:IEnumerable<Environ>
	{
		public int Level{get;}

		public Environ Parent{get;}
		public Type ReturnType{get;}

		public Environ(Type returnType, int level, Environ parent)
		{
			Level = level;
			Parent = parent;
			ReturnType = returnType;
		}
		
		public DeclSeq Decls{get;} = new DeclSeq();

		public Decl FindDecl(string id)
		{
			foreach(Environ e in this)
			{
				if(e.Decls.FindDecl(id) is Decl d)
				{
					if((d.Kind & (DeclKind.Local | DeclKind.Param))==0)
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
		
		public DeclSeq FuncDecls{get;} = new DeclSeq();

		public Decl FindFunc(string s)
		{
			foreach(Environ e in this)
			{
				if(e.FuncDecls.FindDecl(s) is Decl d)
				{
					return d;
				}
			}
			return null;
		}
		
		public DeclSeq TypeDecls{get;} = new DeclSeq();

		public Type FindType(string s)
		{
			switch(s)
			{
				case "%":return Type.Int;
				case "#":return Type.Float;
				case "$":return Type.String;
			}
			foreach(Environ e in this)
			{
				if(e.TypeDecls.FindDecl(s) is Decl d)
				{
					return d.Type as StructType;
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

		public IEnumerator<Environ> GetEnumerator()
		{
			for(Environ e = this; e!=null; e = e.Parent)
			{
				yield return e;
			}
		}
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}