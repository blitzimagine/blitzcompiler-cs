using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Blitz3D.Converter.Parsing.Nodes
{
	public abstract class DeclNode:Node
	{
		public string Comment;

		public string file;

		public abstract void Proto(DeclSeq d, Environ e);

		public abstract IEnumerable<string> WriteData();
	}

	public class CommentDeclNode:DeclNode
	{
		public CommentDeclNode(string comment = null)
		{
			Comment = comment;
		}

		public override void Proto(DeclSeq d, Environ e){}

		public override IEnumerable<string> WriteData()
		{
			yield return Comment;
		}
	}

	//////////////////////////////
	// Sequence of declarations //
	//////////////////////////////
	public class DeclSeqNode:DeclNode, IEnumerable<DeclNode>
	{
		private readonly List<DeclNode> decls = new List<DeclNode>();

		public override void Proto(DeclSeq d, Environ e)
		{
			foreach(DeclNode decl in decls)
			{
				try
				{
					decl.Proto(d, e);
				}
				catch(Ex x)
				{
					if(x.file is null)
					{
						x.file = decl.file;
					}
					throw;
				}
			}
		}
		public override void Semant(Environ e)
		{
			foreach(DeclNode decl in decls)
			{
				try
				{
					decl.Semant(e);
				}
				catch(Ex x)
				{
					if(x.file is null)
					{
						x.file = decl.file;
					}
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

		public void AddComment(string comment)
		{
			if(string.IsNullOrEmpty(comment)){return;}

			if(decls.Count==0 || decls[decls.Count-1].Comment != null)
			{
				Add(new CommentDeclNode());
			}
			decls[decls.Count-1].Comment = comment;
		}

		public int Count => decls.Count;

		public IEnumerator<DeclNode> GetEnumerator() => decls.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => decls.GetEnumerator();
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
		private readonly DECL kind;
		private readonly bool constant;
		private ExprNode defExpr;

		public DeclVarNode sem_var;

		private Type type;

		public VarDeclNode(string i, string t, DECL k, bool c, ExprNode e)
		{
			ident = i;
			tag = t;
			kind = k;
			constant = c;
			defExpr = e;
		}

		public override void Proto(DeclSeq d, Environ e)
		{
			Type ty = tagType(tag, e) ?? Type.Int;
			type = ty;

			if(defExpr!=null)
			{
				defExpr.Semant(e);
				defExpr = defExpr.CastTo(ty, e);
			}

			Decl decl = d.AssertNewDecl(ident, ty, kind, defExpr);
			if(defExpr != null)
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
			if(defExpr != null)
			{
				builder.Append($" = {defExpr.JoinedWriteData()}");
			}
			if(kind != DECL.PARAM)
			{
				builder.Append(';');
				builder.Append(Comment);
			}
			else if(!string.IsNullOrEmpty(Comment))
			{
				throw new Exception();
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
		private readonly string returnTag;

		private readonly DeclSeqNode @params;
		private readonly StmtSeqNode stmts;

		public FuncType sem_type;
		public Environ sem_env;

		public FuncDeclNode(string i, string t, DeclSeqNode p, StmtSeqNode ss)
		{
			ident = i;
			returnTag = t;
			@params = p;
			stmts = ss;
		}

		public override void Proto(DeclSeq d, Environ e)
		{
			Type t = tagType(returnTag, e) ?? Type.Int;
			DeclSeq decls = new DeclSeq();
			@params.Proto(decls, e);
			sem_type = new FuncType(t, decls, false);
			d.AssertNewDecl(ident, sem_type, DECL.FUNC);
		}
		public override void Semant(Environ e)
		{
			sem_env = new Environ(sem_type.returnType, 1, e);
			DeclSeq decls = sem_env.decls;

			int k;
			for(k = 0; k < sem_type.@params.Count; ++k)
			{
				Decl d = sem_type.@params.decls[k];
				decls.insertDecl(d.Name, d.type, d.kind);
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
			yield return $"public static {ret.Name} {ident}({paramStr}){Comment}";
			foreach(string s in stmts.WriteData())
			{
				yield return s;
			}
		}
	}

	//////////////////////
	// Type Declaration //
	//////////////////////
	public class StructDeclNode:DeclNode
	{
		public string Comment_EndStruct;

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
			d.insertDecl(ident, sem_type, DECL.STRUCT);
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
			yield return $"public class {sem_type.Name}{Comment}";
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
		public string WriteData_InstanceDeclaration() => $"public static readonly BlitzData {dataVarName} = new BlitzData();{Comment}";
	}

	////////////////////////
	// Vector declaration //
	////////////////////////
	public class VectorDeclNode:DeclNode
	{
		private readonly string ident;
		private readonly string tag;
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
			d.insertDecl(ident, sem_type, kind);
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
			yield return $"{GetAccessors(kind,true)}{typeName} {ident} = new {elementType}[{exprs.JoinedWriteData()}];{Comment}";
		}
	}
}