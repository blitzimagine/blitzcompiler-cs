using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Blitz3D.Compiling;

namespace Blitz3D.Parsing.Nodes
{
	public abstract class DeclNode:Node
	{
		public Point? pos = null;
		public string file;

		public abstract void Proto(DeclSeq d, Environ e);
		public virtual void Semant(Environ e){}
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
					if(x.pos is null) x.pos = decls[k].pos;
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
					if(x.pos is null) x.pos = decls[k].pos;
					if(x.file.Length==0) x.file = decls[k].file;
					throw;
				}
			}
		}
		
		public override IEnumerable<string> WriteData()
		{
			foreach(var decl in decls)
			{
				foreach(string s in decl.WriteData())
				{
					yield return s;
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

		private Type type;

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
			type = ty;
			ConstType defType = null;

			if(expr!=null)
			{
				expr = expr.Semant(e);
				expr = expr.castTo(ty, e);
				if(constant || (kind & DECL.PARAM)!=0)
				{
					if(!(expr is ConstNode c))
					{
						throw ex("Expression must be constant");
					}
					if(ty == Type.int_type) ty = new ConstType(c.intValue());

					else if(ty == Type.float_type) ty = new ConstType(c.floatValue());

					else ty = new ConstType(c.stringValue());
					e.types.Add(ty);
					expr = null;
				}
				if((kind & DECL.PARAM)!=0)
				{
					defType = (ConstType)ty;
					ty = defType.valueType;
				}
			}
			else if(constant) throw ex("Constants must be initialized");

			Decl decl = d.insertDecl(ident, ty, kind, defType);
			if(decl is null) throw ex("Duplicate variable name");
			if(expr!=null) sem_var = new DeclVarNode(decl);
		}
		public override void Semant(Environ e) { }

		public override IEnumerable<string> WriteData()
		{
			StringBuilder builder = new StringBuilder();
			string accessors = GetAccessors(kind, constant);
			string typeName = type.Name;//Type.FromTag(tag).Name;
			builder.Append($"{accessors}{typeName} {ident}");
			if(expr != null)
			{
				builder.Append($" = {expr.JoinedWriteData()}");
			}
			if(kind != DECL.PARAM)
			{
				builder.Append(';');
			}
			yield return builder.ToString();
		}

		public string WriteData_InitStmtOnly()
		{
			if(expr != null)
			{
				return $"{ident} = {expr.JoinedWriteData()};";
			}
			return null;
		}

		public string WriteData_DeclStmtOnly()
		{
			string accessors = GetAccessors(kind, constant);
			string typeName = type.Name;//Type.FromTag(tag).Name;
			return $"{accessors}{typeName} {ident};";
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
				throw ex("duplicate identifier");
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
				if(decls.insertDecl(d.name, d.type, d.kind) is null) throw ex("duplicate identifier");
			}

			stmts.Semant(sem_env);
		}
		//public override void Translate(Codegen g)
		//{
		//	//var offsets
		//	int size = enumVars(sem_env);

		//	//enter function
		//	g.enter("_f" + ident, size);

		//	//initialize locals
		//	TNode t = createVars(sem_env);
		//	if(t!=null) g.code(t);

		//	//translate statements
		//	stmts.Translate(g);

		//	for(int k = 0; k < sem_env.labels.Count; ++k)
		//	{
		//		if(sem_env.labels[k].def is null)
		//		{
		//			throw ex("Undefined label", sem_env.labels[k].@ref);
		//		}
		//	}

		//	//leave the function
		//	g.label(sem_env.funcLabel + "_leave");
		//	t = deleteVars(sem_env);
		//	g.leave(t, sem_type.@params.Count * 4);
		//}
		public override IEnumerable<string> WriteData()
		{
			Type ret = sem_type.returnType;//Type.FromTag(tag);
			yield return $"public static {ret.Name} {ident}({@params.JoinedWriteData(", ")})";
			yield return "{";
			foreach(string s in stmts.WriteData())
			{
				yield return s;
			}
			yield return "}";
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
				throw ex("Duplicate identifier");
			}
			e.types.Add(sem_type);
		}
		public override void Semant(Environ e)
		{
			fields.Proto(sem_type.fields, e);
			for(int k = 0; k < sem_type.fields.Count; ++k) sem_type.fields.decls[k].offset = k * 4;
		}
		//public override void Translate(Codegen g)
		//{
		//	//translate fields
		//	fields.Translate(g);

		//	//type ID
		//	g.align_data(4);
		//	g.i_data(5, "_t" + ident);

		//	//used and free lists for type
		//	int k;
		//	for(k = 0; k < 2; ++k)
		//	{
		//		string lab = genLabel();
		//		g.i_data(0, lab); //fields
		//		g.p_data(lab); //next
		//		g.p_data(lab); //prev
		//		g.i_data(0); //type
		//		g.i_data(-1); //ref_cnt
		//	}

		//	//number of fields
		//	g.i_data(sem_type.fields.Count);

		//	//type of each field
		//	for(k = 0; k < sem_type.fields.Count; ++k)
		//	{
		//		Decl field = sem_type.fields.decls[k];
		//		Type type = field.type;
		//		string t = null;
		//		if(type == Type.int_type) t = "__bbIntType";
		//		else if(type == Type.float_type) t = "__bbFltType";
		//		else if(type == Type.string_type) t = "__bbStrType";
		//		else if(type.structType() is StructType s) t = "_t" + s.ident;

		//		else if(type.vectorType() is VectorType v) t = v.label;
		//		g.p_data(t);
		//	}
		//}

		public override IEnumerable<string> WriteData()
		{
			yield return $"public class {ident}";
			yield return "{";
			foreach(string s in fields.WriteData())
			{
				yield return s;
			}
			yield return "}";
		}
	}

	//////////////////////
	// Data declaration //
	//////////////////////
	public class DataDeclNode:DeclNode
	{
		public ExprNode expr;
		private string str_label;
		public readonly string dataVarName;

		public DataDeclNode(ExprNode e, string dataVarName)
		{
			expr = e;
			this.dataVarName = dataVarName;
		}

		public override void Proto(DeclSeq d, Environ e)
		{
			expr = expr.Semant(e);
			if(!(expr is ConstNode c))
			{
				throw ex("Data expression must be constant");
			}
			if(expr.sem_type == Type.string_type) str_label = genLabel();
		}
		public override void Semant(Environ e) { }

		public override IEnumerable<string> WriteData()
		{
			string ret = $"{dataVarName}.Add<{expr.sem_type.Name}>({expr.JoinedWriteData()});";
			if(str_label?.Length>0)
			{
				ret = $"/*{str_label}*/{ret}";
			}
			yield return ret;
		}
		public string WriteData_InstanceDeclaration() => $"public static readonly BlitzData {dataVarName} = new BlitzData();";

		//Old genereic type ones
		//public override IEnumerable<string> WriteData()
		//{
		//	yield return $"/*BlitzData<{expr.sem_type.Name}> {str_label}*/ {dataVarName}.Add({expr.JoinedWriteData()});";
		//}
		//public string GetDataInstanceDeclaration() => $"public static readonly BlitzData<{expr.sem_type.Name}> {dataVarName} = new BlitzData<{expr.sem_type.Name}>();";
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
				if(!(e is ConstNode c))
				{
					throw ex("Blitz array sizes must be constant");
				}
				int n = c.intValue();
				if(n < 0) throw ex("Blitz array sizes must not be negative");
				sizes[k] = n + 1;
			}
			string label = genLabel();
			sem_type = new VectorType(label, ty, sizes);
			if(d.insertDecl(ident, sem_type, kind) is null)
			{
				sem_type = null;
				throw ex("Duplicate identifier");
			}
			env.types.Add(sem_type);
		}
		//public override void Translate(Codegen g)
		//{
		//	//type tag!
		//	g.align_data(4);
		//	VectorType v = sem_type.vectorType();
		//	g.i_data(6, v.label);
		//	int sz = 1;
		//	for(int k = 0; k < v.sizes.Length; ++k) sz *= v.sizes[k];
		//	g.i_data(sz);
		//	string t = null;
		//	Type type = v.elementType;
		//	if(type == Type.int_type) t = "__bbIntType";
		//	else if(type == Type.float_type) t = "__bbFltType";
		//	else if(type == Type.string_type) t = "__bbStrType";
		//	else if(type.structType() is StructType s) t = "_t" + s.ident;

		//	else if(type.vectorType() is VectorType v2) t = v2.label;
		//	g.p_data(t);

		//	if(kind == DECL.GLOBAL) g.i_data(0, "_v" + ident);
		//}

		public override IEnumerable<string> WriteData()
		{
			string typeName = sem_type.Name;//Type.FromTag(tag).Name;
			yield return $"{GetAccessors(kind)}{typeName} {ident} = new {typeName}(new {typeName}[]{{{exprs.JoinedWriteData()}}});";
		}

		public string WriteData_InitStmtOnly()
		{
			string typeName = sem_type.Name;//Type.FromTag(tag).Name;
			return $"{ident} = new {typeName}(new {typeName}[]{{{exprs.JoinedWriteData()}}});";
		}

		public string WriteData_DeclStmtOnly()
		{
			string accessors = GetAccessors(kind);
			string typeName = sem_type.Name;//Type.FromTag(tag).Name;
			return $"{accessors}{typeName} {ident};";
		}
	}
}