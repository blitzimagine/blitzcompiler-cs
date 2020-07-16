using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Blitz3D.Converter.Parsing.Nodes
{
	public abstract class DeclNode:Node
	{
		public Point? pos = null;
		public string file;

		public abstract void Proto(DeclSeq d, Environ e);
		public virtual void Semant(Environ e){}

		public abstract IEnumerable<string> WriteData();
	}

	//////////////////////////////
	// Sequence of declarations //
	//////////////////////////////
	public class DeclSeqNode:Node
	{
		public readonly List<DeclNode> decls = new List<DeclNode>();

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
					if(x.pos is null)
					{
						x.pos = decls[k].pos;
					}
					if(x.file is null)
					{
						x.file = decls[k].file;
					}
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
					if(x.pos is null)
					{
						x.pos = decls[k].pos;
					}
					if(x.file is null)
					{
						x.file = decls[k].file;
					}
					throw;
				}
			}
		}
		
		public IEnumerable<string> WriteData()
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
		private readonly string ident;
		private readonly string tag;
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
		}

		public override void Proto(DeclSeq d, Environ e)
		{
			Type ty = tagType(tag, e) ?? Type.Int;
			type = ty;
			//ConstType defType = null;

			if(expr!=null)
			{
				expr.Semant(e);
				expr = expr.CastTo(ty, e);
				//if((kind & DECL.PARAM)!=0)
				//{
				//	defType = (ConstType)ty;
				//	ty = defType.valueType;
				//}
			}

			Decl decl = d.insertDecl(ident, ty, kind, expr);
			if(decl is null)
			{
				throw new Ex("Duplicate variable name");
			}
			if(expr != null)
			{
				sem_var = new BaseDeclVarNode(decl);
			}
		}

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
	}

	//////////////////////////
	// Function Declaration //
	//////////////////////////
	public class FuncDeclNode:DeclNode
	{
		private readonly string ident;
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
			Type t = tagType(tag, e) ?? Type.Int;
			DeclSeq decls = new DeclSeq();
			@params.Proto(decls, e);
			sem_type = new FuncType(t, decls, false, false);
			if(d.insertDecl(ident, sem_type, DECL.FUNC) is null)
			{
				throw new Ex("duplicate identifier");
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
				if(decls.insertDecl(d.Name, d.type, d.kind) is null)
				{
					throw new Ex("duplicate identifier");
				}
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
		//			throw new Ex("Undefined label", sem_env.labels[k].@ref);
		//		}
		//	}

		//	//leave the function
		//	g.label(sem_env.funcLabel + "_leave");
		//	t = deleteVars(sem_env);
		//	g.leave(t, sem_type.@params.Count * 4);
		//}
		public override IEnumerable<string> WriteData()
		{
			Type ret = sem_type.returnType;
			string paramStr = string.Join(", ",@params.WriteData());
			yield return $"public static {ret.Name} {ident}({paramStr})";
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
			sem_type = new StructType(ident);
			if(d.insertDecl(ident, sem_type, DECL.STRUCT) is null)
			{
				throw new Ex("Duplicate identifier");
			}
			e.types.Add(sem_type);
		}
		public override void Semant(Environ e)
		{
			fields.Proto(sem_type.fields, e);
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
			yield return $"public class {sem_type.Name}";
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
			expr.Semant(e);

			if(expr.Sem_Type == Type.String)
			{
				str_label = genLabel();
			}
		}

		public override IEnumerable<string> WriteData()
		{
			string ret = $"{dataVarName}.Add<{expr.Sem_Type.Name}>({expr.JoinedWriteData()});";
			if(str_label?.Length>0)
			{
				ret = $"/*{str_label}*/{ret}";
			}
			yield return ret;
		}
		public string WriteData_InstanceDeclaration() => $"public static readonly BlitzData {dataVarName} = new BlitzData();";
	}

	////////////////////////
	// Vector declaration //
	////////////////////////
	public class VectorDeclNode:DeclNode
	{
		private readonly string ident, tag;
		private readonly ExprSeqNode exprs;
		private readonly DECL kind;
		private VectorType sem_type;
		public VectorDeclNode(string i, string t, ExprSeqNode e, DECL k)
		{
			ident = i;
			tag = t;
			exprs = e;
			kind = k;
		}

		public override void Proto(DeclSeq d, Environ env)
		{
			Type ty = tagType(tag, env) ?? Type.Int;

			sem_type = new VectorType(ty, exprs.Count);
			if(d.insertDecl(ident, sem_type, kind) is null)
			{
				throw new Ex("Duplicate identifier");
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
			string typeName = sem_type.Name;
			string elementType = sem_type.elementType.Name;
			yield return $"{GetAccessors(kind,true)}{typeName} {ident} = new {elementType}[{exprs.JoinedWriteData()}];";
		}
	}
}