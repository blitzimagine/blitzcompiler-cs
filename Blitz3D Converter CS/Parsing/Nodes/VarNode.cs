using System.Collections.Generic;
using Blitz3D.Compiling;

namespace Blitz3D.Parsing.Nodes
{
	public abstract class VarNode:Node
	{
		public Type sem_type;

		//get set var
		//////////////////////////////////
		// Common get/set for variables //
		//////////////////////////////////
		//public TNode load(Codegen g)
		//{
		//	TNode t = Translate(g);
		//	if(sem_type == Type.string_type) return call("__bbStrLoad", t);
		//	return mem(t);
		//}
		//public virtual TNode store(Codegen g, TNode n)
		//{
		//	TNode t = Translate(g);
		//	if(sem_type.structType()!=null) return call("__bbObjStore", t, n);
		//	if(sem_type == Type.string_type) return call("__bbStrStore", t, n);
		//	return move(n, mem(t));
		//}
		public virtual bool isObjParam() => false;

		//addr of var
		public virtual void Semant(Environ e){}

		public abstract string JoinedWriteData();
	}

	//////////////////
	// Declared var //
	//////////////////
	public class DeclVarNode:VarNode
	{
		public Decl sem_decl;

		public DeclVarNode(Decl d = null)
		{
			sem_decl = d;
			if(d!=null)
			{
				sem_type = d.type;
			}
		}

		//public override TNode Translate(Codegen g)
		//{
		//	if(sem_decl.kind == DECL.GLOBAL)
		//	{
		//		return global("_v" + sem_decl.name);
		//	}
		//	return local(sem_decl.offset);
		//}
		//public override TNode store(Codegen g, TNode n)
		//{
		//	if(isObjParam())
		//	{
		//		TNode t = Translate(g);
		//		return move(n, mem(t));
		//	}
		//	return base.store(g, n);
		//}
		public override bool isObjParam() => sem_type is StructType && sem_decl.kind == DECL.PARAM;

		public override string JoinedWriteData() => sem_decl.name;
	}

	///////////////
	// Ident var //
	///////////////
	public class IdentVarNode:DeclVarNode
	{
		private readonly string ident;
		private readonly string tag;
		public IdentVarNode(string i, string t)
		{
			ident = i;
			tag = t;
		}
		public override void Semant(Environ e)
		{
			if(sem_decl!=null) return;
			Type t = tagType(tag, e) ?? Type.Int;
			if((sem_decl = e.findDecl(ident))!=null)
			{
				if((sem_decl.kind & (DECL.GLOBAL | DECL.LOCAL | DECL.PARAM))==0)
				{
					throw new Ex("Identifier '" + sem_decl.name + "' may not be used like this");
				}
				Type ty = sem_decl.type;
				if(ty is ConstType constType)
				{
					ty = constType.valueType;
				}
				if(tag.Length>0 && t != ty) throw new Ex("Variable type mismatch");
			}
			else
			{
				//ugly auto decl!
				sem_decl = e.decls.insertDecl(ident, t, DECL.LOCAL);
			}
			sem_type = sem_decl.type;
		}

		public override string JoinedWriteData() => ident;
	}

	/////////////////
	// Indexed Var //
	/////////////////
	public class ArrayVarNode:VarNode
	{
		private readonly string ident;
		private readonly string tag;
		private readonly ExprSeqNode exprs;
		private Decl sem_decl;

		public ArrayVarNode(string i, string t, ExprSeqNode e)
		{
			ident = i;
			tag = t;
			exprs = e;
		}

		public override void Semant(Environ e)
		{
			exprs.Semant(e);
			exprs.CastTo(Type.Int, e);
			Type t = e.findType(tag);
			sem_decl = e.findDecl(ident);
			if(sem_decl is null || (sem_decl.kind & DECL.ARRAY)==0)
			{
				throw new Ex("Array not found");
			}
			ArrayType a = (ArrayType)sem_decl.type;
			if(t!=null && t != a.elementType)
			{
				throw new Ex("array type mismtach");
			}
			if(a.dims != exprs.Count)
			{
				throw new Ex("incorrect number of dimensions");
			}
			sem_type = a.elementType;
		}

		public override string JoinedWriteData() => $"{ident}[{exprs.JoinedWriteData()}]";
	}

	///////////////
	// Field var //
	///////////////
	public class FieldVarNode:VarNode
	{
		public ExprNode expr;
		public readonly string ident, tag;
		public Decl sem_field;
		public FieldVarNode(ExprNode e, string i, string t)
		{
			expr = e;
			ident = i;
			tag = t;
		}

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			if(!(expr.sem_type is StructType s))
			{
				throw new Ex("Variable must be a Type");
			}
			sem_field = s.fields.findDecl(ident);
			if(sem_field is null)
			{
				throw new Ex("Type field not found");
			}
			sem_type = sem_field.type;
		}

		public override string JoinedWriteData() => $"{expr.JoinedWriteData()}.{ident}";
	}

	////////////////
	// Vector var //
	////////////////
	public class VectorVarNode:VarNode
	{
		public ExprNode expr;
		public readonly ExprSeqNode exprs;
		public VectorType vec_type;
		public VectorVarNode(ExprNode e, ExprSeqNode es)
		{
			expr = e;
			exprs = es;
		}

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			if(!(expr.sem_type is VectorType vec_type))
			{
				throw new Ex("Variable must be a Blitz array");
			}
			if(vec_type.dimensions != exprs.Count)
			{
				throw new Ex("Incorrect number of subscripts");
			}
			exprs.Semant(e);
			exprs.CastTo(Type.Int, e);
			sem_type = vec_type.elementType;
		}
		//public override TNode Translate(Codegen g)
		//{
		//	int sz = 4;
		//	TNode t = null;
		//	for(int k = 0; k < exprs.Count; ++k)
		//	{
		//		TNode p;
		//		ExprNode e = exprs.exprs[k];
		//		if(e is ConstNode t2)
		//		{
		//			p = iconst(t2.intValue() * sz);
		//		}
		//		else
		//		{
		//			p = e.Translate(g);
		//			p = mul(p, iconst(sz));
		//		}
		//		sz *= vec_type.sizes[k];
		//		t = t!=null ? add(t, p) : p;
		//	}
		//	return add(t, expr.Translate(g));
		//}

		public override string JoinedWriteData() => $"{expr.JoinedWriteData()}[{exprs.JoinedWriteData()}]";
	}
}