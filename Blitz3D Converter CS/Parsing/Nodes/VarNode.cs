namespace Blitz3D.Converter.Parsing.Nodes
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

		public abstract string JoinedWriteData();
	}

	//////////////////
	// Declared var //
	//////////////////
	public class DeclVarNode:VarNode
	{
		public Decl sem_decl;

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

		public override string JoinedWriteData() => sem_decl.Name;
	}

	public class BaseDeclVarNode:DeclVarNode
	{
		public BaseDeclVarNode(Decl d = null)
		{
			sem_decl = d;
			sem_type = d?.type;
		}

		public override string JoinedWriteData() => sem_decl.Name;
	}

	///////////////
	// Ident var //
	///////////////
	public class IdentVarNode:DeclVarNode
	{
		private readonly string ident;
		private readonly string tag;
		private bool declaration = false;

		public IdentVarNode(string i, string t)
		{
			ident = i;
			tag = t;
		}
		public override void Semant(Environ e)
		{
			if(sem_decl!=null)
			{
				return;
			}
			Type t = tagType(tag, e) ?? Type.Int;
			sem_decl = e.FindDecl(ident);
			if(sem_decl!=null)
			{
				Type ty = sem_decl.type;
				//if(ty is ConstType constType)
				//{
				//	ty = constType.valueType;
				//}
				if(tag.Length>0 && t != ty)
				{
					throw new Ex("Variable type mismatch");
				}
				declaration = false;
			}
			else
			{
				//ugly auto decl!
				sem_decl = e.decls.insertDecl(ident, t, DECL.LOCAL);
				declaration = true;
			}
			sem_type = sem_decl.type;
		}

		public override string JoinedWriteData()
		{
			if(declaration)
			{
				return $"{sem_type.Name} {sem_decl.Name}";
			}
			return sem_decl.Name;
		}
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
			Type t = e.FindType(tag);
			sem_decl = e.FindDecl(ident);
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

		public override string JoinedWriteData()
		{
			#region OpenWA only stuff
			if(sem_decl.Name == "GameObject")
			{
				return $"GameObjects[{exprs.JoinedWriteData()}]";
			}
			#endregion
			return $"{sem_decl.Name}[{exprs.JoinedWriteData()}]";
		}
	}

	///////////////
	// Field var //
	///////////////
	public class FieldVarNode:VarNode
	{
		private readonly ExprNode expr;
		private readonly string ident;
		private readonly string tag;

		private Decl sem_field;

		public FieldVarNode(ExprNode e, string i, string t)
		{
			expr = e;
			ident = i;
			tag = t;
		}

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			sem_field = ((StructType)expr.Sem_Type).fields.findDecl(ident);
			if(sem_field is null)
			{
				throw new Ex("Type field not found");
			}
			sem_type = sem_field.type;
		}

		public override string JoinedWriteData() => $"{expr.JoinedWriteData()}.{sem_field.Name}";
	}

	////////////////
	// Vector var //
	////////////////
	public class VectorVarNode:VarNode
	{
		private readonly ExprNode expr;
		private readonly ExprSeqNode exprs;

		public VectorVarNode(ExprNode e, ExprSeqNode es)
		{
			expr = e;
			exprs = es;
		}

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			if(!(expr.Sem_Type is VectorType vec_type))
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

		public override string JoinedWriteData() => $"{expr.JoinedWriteData()}[{exprs.JoinedWriteData()}]";
	}
}