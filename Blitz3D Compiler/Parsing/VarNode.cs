using System.Collections.Generic;
using Blitz3D.Compiling;

namespace Blitz3D.Parsing
{
	public abstract class VarNode:Node
	{
		public Type sem_type;

		//get set var
		//////////////////////////////////
		// Common get/set for variables //
		//////////////////////////////////
		public TNode load(Codegen g)
		{
			TNode t = Translate(g);
			if(sem_type == Type.string_type) return call("__bbStrLoad", t);
			return mem(t);
		}
		public virtual TNode store(Codegen g, TNode n)
		{
			TNode t = Translate(g);
			if(sem_type.structType()!=null) return call("__bbObjStore", t, n);
			if(sem_type == Type.string_type) return call("__bbStrStore", t, n);
			return move(n, mem(t));
		}
		public virtual bool isObjParam() => false;

		//addr of var
		public abstract void Semant(Environ e);
		public abstract TNode Translate(Codegen g);
	}
	//#include "decl.h"


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

		public override void Semant(Environ e) { }

		public override TNode Translate(Codegen g)
		{
			if(sem_decl.kind == DECL.GLOBAL)
			{
				return global("_v" + sem_decl.name);
			}
			return local(sem_decl.offset);
		}
		public override TNode store(Codegen g, TNode n)
		{
			if(isObjParam())
			{
				TNode t = Translate(g);
				return move(n, mem(t));
			}
			return base.store(g, n);
		}
		public override bool isObjParam() => sem_type.structType()!=null && sem_decl.kind == DECL.PARAM;

		public override IEnumerable<string> WriteData()
		{
			yield return sem_decl.name;
		}
	}

	///////////////
	// Ident var //
	///////////////
	public class IdentVarNode:DeclVarNode
	{
		public readonly string ident, tag;
		public IdentVarNode(string i, string t)
		{
			ident = i;
			tag = t;
		}
		public override void Semant(Environ e)
		{
			if(sem_decl!=null) return;
			Type t = tagType(tag, e);
			if(t is null) t = Type.int_type;
			if((sem_decl = e.findDecl(ident))!=null)
			{
				if((sem_decl.kind & (DECL.GLOBAL | DECL.LOCAL | DECL.PARAM))==0)
				{
					ex("Identifier '" + sem_decl.name + "' may not be used like this");
				}
				Type ty = sem_decl.type;
				if(ty.constType()!=null)
				{
					ty = ty.constType().valueType;
				}
				if(tag.Length>0 && t != ty) ex("Variable type mismatch");
			}
			else
			{
				//ugly auto decl!
				sem_decl = e.decls.insertDecl(ident, t, DECL.LOCAL);
			}
			sem_type = sem_decl.type;
		}

		public override IEnumerable<string> WriteData()
		{
			yield return ident;
		}
	}

	/////////////////
	// Indexed Var //
	/////////////////
	public class ArrayVarNode:VarNode
	{
		public readonly string ident, tag;
		public readonly ExprSeqNode exprs;
		public Decl sem_decl;
		public ArrayVarNode(string i, string t, ExprSeqNode e)
		{
			ident = i;
			tag = t;
			exprs = e;
		}

		public override void Semant(Environ e)
		{
			exprs.semant(e);
			exprs.castTo(Type.int_type, e);
			Type t = e.findType(tag);
			sem_decl = e.findDecl(ident);
			if(sem_decl is null || (sem_decl.kind & DECL.ARRAY)==0) ex("Array not found");
			ArrayType a = sem_decl.type.arrayType();
			if(t!=null && t != a.elementType) ex("array type mismtach");
			if(a.dims != exprs.Count) ex("incorrect number of dimensions");
			sem_type = a.elementType;
		}
		public override TNode Translate(Codegen g)
		{
			TNode t = null;
			for(int k = 0; k < exprs.Count; ++k)
			{
				TNode e = exprs.exprs[k].Translate(g);
				if(k!=0)
				{
					TNode s = mem(add(global("_a" + ident), iconst(k * 4 + 8)));
					e = add(t, mul(e, s));
				}
				t = e;
			}
			t = add(mem(global("_a" + ident)), mul(t, iconst(4)));
			return t;
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"{ident}[{exprs.JoinedWriteData()}]";
		}
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
			expr = expr.Semant(e);
			StructType s = expr.sem_type.structType();
			if(s is null) ex("Variable must be a Type");
			sem_field = s.fields.findDecl(ident);
			if(sem_field is null) ex("Type field not found");
			sem_type = sem_field.type;
		}
		public override TNode Translate(Codegen g)
		{
			TNode t = expr.Translate(g);
			t = mem(t);
			return add(t, iconst(sem_field.offset));
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"this.{ident}";
		}
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
			expr = expr.Semant(e);
			vec_type = expr.sem_type.vectorType();
			if(vec_type is null) ex("Variable must be a Blitz array");
			if(vec_type.sizes.Length != exprs.Count) ex("Incorrect number of subscripts");
			exprs.semant(e);
			exprs.castTo(Type.int_type, e);
			for(int k = 0; k < exprs.Count; ++k)
			{
				ConstNode t = exprs.exprs[k].constNode();
				if(t!=null)
				{
					if(t.intValue() >= vec_type.sizes[k])
					{
						ex("Blitz array subscript out of range");
					}
				}
			}
			sem_type = vec_type.elementType;
		}
		public override TNode Translate(Codegen g)
		{
			int sz = 4;
			TNode t = null;
			for(int k = 0; k < exprs.Count; ++k)
			{
				TNode p;
				ExprNode e = exprs.exprs[k];
				if(e.constNode() is ConstNode t2)
				{
					p = iconst(t2.intValue() * sz);
				}
				else
				{
					p = e.Translate(g);
					p = mul(p, iconst(sz));
				}
				sz *= vec_type.sizes[k];
				t = t!=null ? add(t, p) : p;
			}
			return add(t, expr.Translate(g));
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"{expr.JoinedWriteData()}[{exprs.JoinedWriteData()}]";
		}
	}
}