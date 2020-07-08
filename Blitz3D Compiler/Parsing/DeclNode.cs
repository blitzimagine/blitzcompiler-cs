using System;
using System.Collections.Generic;
using Blitz3D.Compiling;
using Blitz3D.Compiling;

namespace Blitz3D.Parsing
{
	public class DeclNode:Node
	{
		public int pos;
		public string file;
		public DeclNode() { pos = -1; }
		public virtual void Proto(DeclSeq d, Environ e) { }
		public virtual void Semant(Environ e) { }
		public virtual void Translate(Codegen g) { }
		public virtual void transdata(Codegen g) { }
	}

	//////////////////////////////
	// Sequence of declarations //
	//////////////////////////////
	public class DeclSeqNode:Node
	{
		public readonly List<DeclNode> decls = new List<DeclNode>();
		public DeclSeqNode() { }

		public void Proto(DeclSeq d, Environ e)
		{
			for(int k = 0; k < decls.Count; ++k)
			{
				try
				{
					decls[k].Proto(d, e);
				}
				catch(Ex x)
				{
					if(x.pos < 0) x.pos = decls[k].pos;
					if(x.file.Length==0) x.file = decls[k].file;
					throw;
				}
			}
		}
		public void Semant(Environ e)
		{
			for(int k = 0; k < decls.Count; ++k)
			{
				try
				{
					decls[k].Semant(e);
				}
				catch(Ex x)
				{
					if(x.pos < 0) x.pos = decls[k].pos;
					if(x.file.Length==0) x.file = decls[k].file;
					throw;
				}
			}
		}
		public void Translate(Codegen g)
		{
			for(int k = 0; k < decls.Count; ++k)
			{
				try
				{
					decls[k].Translate(g);
				}
				catch(Ex x)
				{
					if(x.pos < 0) x.pos = decls[k].pos;
					if(x.file.Length==0) x.file = decls[k].file;
					throw;
				}
			}
		}
		public void transdata(Codegen g)
		{
			for(int k = 0; k < decls.Count; ++k)
			{
				try
				{
					decls[k].transdata(g);
				}
				catch(Ex x)
				{
					if(x.pos < 0) x.pos = decls[k].pos;
					if(x.file.Length==0) x.file = decls[k].file;
					throw;
				}
			}
		}

		public void Add(DeclNode d) => decls.Add(d);

		public int Count => decls.Count;
	}

	//'kind' shouldn't really be in Parser...
	//should probably be LocalDeclNode,GlobalDeclNode,ParamDeclNode
	////////////////////////////
	// Simple var declaration //
	////////////////////////////
	public class VarDeclNode:DeclNode
	{
		public string ident, tag;
		public DECL kind;
		public bool constant;
		public ExprNode expr;
		public DeclVarNode sem_var;

		public VarDeclNode(string i, string t, DECL k, bool c, ExprNode e)
		{
			ident = i;
			tag = t;
			kind = k;
			constant = c;
			expr = e;
			sem_var = null;
		}

		public override void Proto(DeclSeq d, Environ e)
		{
			Type ty = tagType(tag, e);
			if(ty is null) ty = Type.int_type;
			ConstType defType = null;

			if(expr!=null)
			{
				expr = expr.Semant(e);
				expr = expr.castTo(ty, e);
				if(constant || (kind & DECL.PARAM)!=0)
				{
					ConstNode c = expr.constNode();
					if(c is null) ex("Expression must be constant");
					if(ty == Type.int_type) ty = new ConstType(c.intValue());

					else if(ty == Type.float_type) ty = new ConstType(c.floatValue());

					else ty = new ConstType(c.stringValue());
					e.types.Add(ty);
					expr = null;
				}
				if((kind & DECL.PARAM)!=0)
				{
					defType = ty.constType();
					ty = defType.valueType;
				}
			}
			else if(constant) ex("Constants must be initialized");

			Decl decl = d.insertDecl(ident, ty, kind, defType);
			if(decl is null) ex("Duplicate variable name");
			if(expr!=null) sem_var = new DeclVarNode(decl);
		}
		public override void Semant(Environ e) { }
		public override void Translate(Codegen g)
		{
			if((kind & DECL.GLOBAL)!=0)
			{
				g.align_data(4);
				g.i_data(0, "_v" + ident);
			}
			if(expr!=null) g.code(sem_var.store(g, expr.Translate(g)));
		}
	}

	//////////////////////////
	// Function Declaration //
	//////////////////////////
	public class FuncDeclNode:DeclNode
	{
		public readonly string ident;
		/// <summary>Return type</summary>
		public readonly string tag;
		public DeclSeqNode @params;
		public StmtSeqNode stmts;
		public FuncType sem_type;
		public Environ sem_env;

		public FuncDeclNode(string i, string t, DeclSeqNode p, StmtSeqNode ss)
		{
			ident = i;
			tag = t;
			@params = p;
			stmts = ss;
		}

		public override void Proto(DeclSeq d, Environ e)
		{
			Type t = tagType(tag, e);
			if(t is null) t = Type.int_type;
			DeclSeq decls = new DeclSeq();
			@params.Proto(decls, e);
			sem_type = new FuncType(t, decls, false, false);
			if(d.insertDecl(ident, sem_type, DECL.FUNC) is null)
			{
				sem_type = null;
				ex("duplicate identifier");
			}
			e.types.Add(sem_type);
		}
		public override void Semant(Environ e)
		{
			sem_env = new Environ(genLabel(), sem_type.returnType, 1, e);
			DeclSeq decls = sem_env.decls;

			int k;
			for(k = 0; k < sem_type.@params.Count; ++k)
			{
				Decl d = sem_type.@params.decls[k];
				if(decls.insertDecl(d.name, d.type, d.kind) is null) ex("duplicate identifier");
			}

			stmts.Semant(sem_env);
		}
		public override void Translate(Codegen g)
		{
			//var offsets
			int size = enumVars(sem_env);

			//enter function
			g.enter("_f" + ident, size);

			//initialize locals
			TNode t = createVars(sem_env);
			if(t!=null) g.code(t);

			//translate statements
			stmts.Translate(g);

			for(int k = 0; k < sem_env.labels.Count; ++k)
			{
				if(sem_env.labels[k].def < 0) ex("Undefined label", sem_env.labels[k].@ref);
			}

			//leave the function
			g.label(sem_env.funcLabel + "_leave");
			t = deleteVars(sem_env);
			g.leave(t, sem_type.@params.Count * 4);
		}
	}

	//////////////////////
	// Type Declaration //
	//////////////////////
	public class StructDeclNode:DeclNode
	{
		public string ident;
		public DeclSeqNode fields;
		public StructType sem_type;
		public StructDeclNode(string i, DeclSeqNode f)
		{
			ident = i;
			fields = f;
		}

		public override void Proto(DeclSeq d, Environ e)
		{
			sem_type = new StructType(ident, new DeclSeq());
			if(d.insertDecl(ident, sem_type, DECL.STRUCT) is null)
			{
				sem_type = null;
				ex("Duplicate identifier");
			}
			e.types.Add(sem_type);
		}
		public override void Semant(Environ e)
		{
			fields.Proto(sem_type.fields, e);
			for(int k = 0; k < sem_type.fields.Count; ++k) sem_type.fields.decls[k].offset = k * 4;
		}
		public override void Translate(Codegen g)
		{
			//translate fields
			fields.Translate(g);

			//type ID
			g.align_data(4);
			g.i_data(5, "_t" + ident);

			//used and free lists for type
			int k;
			for(k = 0; k < 2; ++k)
			{
				string lab = genLabel();
				g.i_data(0, lab); //fields
				g.p_data(lab); //next
				g.p_data(lab); //prev
				g.i_data(0); //type
				g.i_data(-1); //ref_cnt
			}

			//number of fields
			g.i_data(sem_type.fields.Count);

			//type of each field
			for(k = 0; k < sem_type.fields.Count; ++k)
			{
				Decl field = sem_type.fields.decls[k];
				Type type = field.type;
				string t = null;
				if(type == Type.int_type) t = "__bbIntType";
				else if(type == Type.float_type) t = "__bbFltType";
				else if(type == Type.string_type) t = "__bbStrType";
				else if(type.structType() is StructType s) t = "_t" + s.ident;

				else if(type.vectorType() is VectorType v) t = v.label;
				g.p_data(t);
			}
		}
	}

	//////////////////////
	// Data declaration //
	//////////////////////
	public class DataDeclNode:DeclNode
	{
		public ExprNode expr;
		public string str_label;
		public DataDeclNode(ExprNode e) { expr = e; }

		public override void Proto(DeclSeq d, Environ e)
		{
			expr = expr.Semant(e);
			ConstNode c = expr.constNode();
			if(c is null) ex("Data expression must be constant");
			if(expr.sem_type == Type.string_type) str_label = genLabel();
		}
		public override void Semant(Environ e) { }
		public override void Translate(Codegen g)
		{
			if(expr.sem_type != Type.string_type) return;
			ConstNode c = expr.constNode();
			g.s_data(c.stringValue(), str_label);
		}
		public override void transdata(Codegen g)
		{
			ConstNode c = expr.constNode();
			if(expr.sem_type == Type.int_type)
			{
				g.i_data(1);
				g.i_data(c.intValue());
			}
			else if(expr.sem_type == Type.float_type)
			{
				float n = c.floatValue();
				g.i_data(2);
				g.i_data(BitConverter.SingleToInt32Bits(n));
			}
			else
			{
				g.i_data(4);
				g.p_data(str_label);
			}
		}
	}

	////////////////////////
	// Vector declaration //
	////////////////////////
	public class VectorDeclNode:DeclNode
	{
		public string ident, tag;
		public ExprSeqNode exprs;
		public DECL kind;
		public VectorType sem_type;
		public VectorDeclNode(string i, string t, ExprSeqNode e, DECL k)
		{
			ident = i;
			tag = t;
			exprs = e;
			kind = k;
		}

		public override void Proto(DeclSeq d, Environ env)
		{
			Type ty = tagType(tag, env);
			if(ty is null) ty = Type.int_type;

			int[] sizes = new int[exprs.Count];
			for(int k = 0; k < exprs.Count; ++k)
			{
				ExprNode e = exprs.exprs[k] = exprs.exprs[k].Semant(env);
				ConstNode c = e.constNode();
				if(c is null) ex("Blitz array sizes must be constant");
				int n = c.intValue();
				if(n < 0) ex("Blitz array sizes must not be negative");
				sizes[k] = n + 1;
			}
			string label = genLabel();
			sem_type = new VectorType(label, ty, sizes);
			if(d.insertDecl(ident, sem_type, kind) is null)
			{
				sem_type = null;
				ex("Duplicate identifier");
			}
			env.types.Add(sem_type);
		}
		public override void Translate(Codegen g)
		{
			//type tag!
			g.align_data(4);
			VectorType v = sem_type.vectorType();
			g.i_data(6, v.label);
			int sz = 1;
			for(int k = 0; k < v.sizes.Length; ++k) sz *= v.sizes[k];
			g.i_data(sz);
			string t = null;
			Type type = v.elementType;
			if(type == Type.int_type) t = "__bbIntType";
			else if(type == Type.float_type) t = "__bbFltType";
			else if(type == Type.string_type) t = "__bbStrType";
			else if(type.structType() is StructType s) t = "_t" + s.ident;

			else if(type.vectorType() is VectorType v2) t = v2.label;
			g.p_data(t);

			if(kind == DECL.GLOBAL) g.i_data(0, "_v" + ident);
		}
	}
}