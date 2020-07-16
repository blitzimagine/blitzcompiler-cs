using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Blitz3D.Converter.Parsing.Nodes
{
	public abstract class _StmtNode:Node
	{
		public abstract IEnumerable<string> WriteData();
	}

	public abstract class StmtNode:_StmtNode
	{
		public Point? pos = null; //offset in source stream

		public virtual void Semant(Environ e){}
	}

	////////////////////////
	// Statement Sequence //
	////////////////////////
	public class StmtSeqNode:_StmtNode
	{
		public readonly string file;
		public readonly List<StmtNode> stmts = new List<StmtNode>();
		public StmtSeqNode(string f)
		{
			file = f;
		}

		public void Semant(Environ e)
		{
			for(int k = 0; k < stmts.Count; ++k)
			{
				try
				{
					stmts[k].Semant(e);
				}
				catch(Ex x)
				{
					if(x.pos is null)
					{
						x.pos = stmts[k].pos;
					}
					if(x.file is null)
					{
						x.file = file;
					}
					throw;
				}
			}
		}
		//public void Translate(Codegen g)
		//{
		//	for(int k = 0; k < stmts.Count; ++k)
		//	{
		//		StmtNode stmt = stmts[k];
		//		try
		//		{
		//			stmt.Translate(g);
		//		}
		//		catch(Ex x)
		//		{
		//			if(x.pos is null) x.pos = stmts[k].pos;
		//			if(x.file.Length==0) x.file = file;
		//			throw;
		//		}
		//	}
		//}

		public void Add(StmtNode s) => stmts.Add(s);

		public int Count => stmts.Count;

		public override IEnumerable<string> WriteData()
		{
			foreach(var stmt in stmts)
			{
				foreach(string s in stmt.WriteData())
				{
					yield return s;
				}
			}
		}
	}

	/////////////////
	// An Include! //
	/////////////////
	public class IncludeNode:StmtNode
	{
		private readonly FileNode include;

		public IncludeNode(FileNode ss)
		{
			include = ss;
		}

		public override void Semant(Environ e) => include.stmts.Semant(e);

		public override IEnumerable<string> WriteData()
		{
			//TODO: Store include file data
			yield return $"using static {Path.ChangeExtension(include.fileName,null).Replace('-','_')};";
		}
	}

	///////////////////
	// a declaration //
	///////////////////
	public class DeclStmtNode:StmtNode
	{
		private readonly DeclNode decl;

		public DeclStmtNode(DeclNode d)
		{
			decl = d;
			pos = d.pos;
		}

		public override void Semant(Environ e)
		{
			decl.Proto(e.decls, e);
			decl.Semant(e);
		}

		public override IEnumerable<string> WriteData() => decl.WriteData();
	}

	//////////////////////////////
	// Dim AND declare an Array //
	//////////////////////////////
	public class DimNode:StmtNode
	{
		private readonly string ident;
		private readonly string tag;
		private ExprSeqNode exprs;
		private ArrayType sem_type;
		private Decl sem_decl;
		public DimNode(string i, string t, ExprSeqNode e)
		{
			ident = i;
			tag = t;
			exprs = e;
		}

		public override void Semant(Environ e)
		{
			Type t = tagType(tag,e);
			if(e.findDecl(ident) is Decl d)
			{
				if(!(d.type is ArrayType a) || a.dims != exprs.Count || (t!=null && a.elementType != t))
				{
					throw new Ex("Duplicate identifier");
				}
				sem_type = a;
				sem_decl = null;
			}
			else
			{
				if(e.level > 0) throw new Ex("Array not found in main program");
				sem_type = new ArrayType(t ?? Type.Int, exprs.Count);
				sem_decl = e.decls.insertDecl(ident, sem_type, DECL.ARRAY);
				e.types.Add(sem_type);
			}
			exprs.Semant(e);
			exprs.CastTo(Type.Int, e);
		}
		//public override void Translate(Codegen g)
		//{
		//	g.code(call("__bbUndimArray", global("_a" + ident)));
		//	for(int k = 0; k < exprs.Count; ++k)
		//	{
		//		TNode t = add(global("_a" + ident), iconst(k * 4 + 12));
		//		t = move(exprs.exprs[k].Translate(g), mem(t));
		//		g.code(t);
		//	}
		//	g.code(call("__bbDimArray", global("_a" + ident)));

		//	if(sem_decl is null) return;

		//	int et;
		//	Type ty = sem_type.arrayType().elementType;
		//	if(ty == Type.int_type) et = 1;
		//	else if(ty == Type.float_type) et = 2;
		//	else if(ty == Type.string_type) et = 3;
		//	else et = 5;

		//	g.align_data(4);
		//	g.i_data(0, "_a" + ident);
		//	g.i_data(et);
		//	g.i_data(exprs.Count);
		//	for(int k = 0; k < exprs.Count; ++k) g.i_data(0);
		//}

		public override IEnumerable<string> WriteData()
		{
			yield return $"{sem_type.Name} {ident} = new {sem_type.elementType.Name}[{exprs.JoinedWriteData()}];";
		}
	}

	////////////////
	// Assignment //
	////////////////
	public class AsgnNode:StmtNode
	{
		private VarNode var;
		private ExprNode expr;
		public AsgnNode(VarNode var, ExprNode expr)
		{
			this.var = var;
			this.expr = expr;
		}

		public override void Semant(Environ e)
		{
			var.Semant(e);
			if(var.sem_type is ConstType) throw new Ex("Constants can not be assigned to");
			if(var.sem_type is VectorType) throw new Ex("Blitz arrays can not be assigned to");
			expr.Semant(e);
			expr = expr.CastTo(var.sem_type, e);
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"{var.JoinedWriteData()} = {expr.JoinedWriteData()};";
		}
	}

	//////////////////////////
	// Expression statement //
	//////////////////////////
	public class ExprStmtNode:StmtNode
	{
		private readonly ExprNode expr;
		public ExprStmtNode(ExprNode e)
		{
			expr = e;
		}

		public override void Semant(Environ e) => expr.Semant(e);

		public override IEnumerable<string> WriteData()
		{
			yield return $"{expr.JoinedWriteData()};";
		}
	}

	////////////////
	// user label //
	////////////////
	public class LabelNode:StmtNode
	{
		private readonly string ident;
		private Label sem_label;

		public LabelNode(string s)
		{
			ident = s;
		}
		public override void Semant(Environ e)
		{
			sem_label = e.DefineLabel(ident, ident);
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"{sem_label.Name}:;";
		}
	}

	////////////////////
	// Goto statement //
	////////////////////
	public class GotoNode:StmtNode
	{
		private string ident;
		private Label sem_label;

		public GotoNode(string s)
		{
			ident=s;
		}
		public override void Semant(Environ e)
		{
			sem_label = e.GetLabel(ident);
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"goto {sem_label};";
		}
	}

	/////////////////////
	// Gosub statement //
	/////////////////////
	///<summary>JSR/Jump subroutine</summary>
	public class GosubNode:StmtNode
	{
		private string ident;
		private Label sem_label;
		public GosubNode(string s)
		{
			ident = s;
		}
		public override void Semant(Environ e)
		{
			sem_label = e.GetLabel(ident);
		}

		public override IEnumerable<string> WriteData() => throw new NotSupportedException();
	}

	//////////////////
	// If statement //
	//////////////////
	public class IfNode:StmtNode
	{
		private ExprNode expr;
		private StmtSeqNode stmts;
		private StmtSeqNode elseOpt;
		public IfNode(ExprNode e, StmtSeqNode s, StmtSeqNode o)
		{
			expr = e;
			stmts = s;
			elseOpt = o;
		}

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			expr = expr.CastTo(Type.Int, e);
			stmts.Semant(e);
			elseOpt?.Semant(e);
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"if({expr.JoinedWriteData()})";
			yield return "{";
			foreach(string s in stmts.WriteData())
			{
				yield return s;
			}
			yield return "}";
			if(elseOpt!=null)
			{
				if(elseOpt.Count == 1 && elseOpt.stmts[0] is IfNode elseIf)
				{
					IEnumerator<string> elseIfEnumerator = elseIf.WriteData().GetEnumerator();
					elseIfEnumerator.MoveNext();
					yield return "else "+elseIfEnumerator.Current;
					while(elseIfEnumerator.MoveNext())
					{
						yield return elseIfEnumerator.Current;
					}
				}
				else
				{
					yield return "else";
					yield return "{";
					foreach(string s in elseOpt.WriteData())
					{
						yield return s;
					}
					yield return "}";
				}
			}
		}
	}

	///////////
	// Break //
	///////////
	public class ExitNode:StmtNode
	{
		public override IEnumerable<string> WriteData()
		{
			yield return $"break;";
		}
	}

	/////////////////////
	// While statement //
	/////////////////////
	public class WhileNode:StmtNode
	{
		private ExprNode expr;
		private readonly StmtSeqNode stmts;

		public WhileNode(ExprNode e, StmtSeqNode s)
		{
			expr = e;
			stmts = s;
		}

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			expr = expr.CastTo(Type.Int, e);
			stmts.Semant(e);
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"while({expr.JoinedWriteData()})";
			yield return "{";
			foreach(string s in stmts.WriteData())
			{
				yield return s;
			}
			yield return "}";
		}
	}

	///////////////////
	// For/Next loop //
	///////////////////
	public class ForNode:StmtNode
	{
		private readonly VarNode var;
		private ExprNode fromExpr,toExpr,stepExpr;
		private readonly StmtSeqNode stmts;
		public ForNode(VarNode var, ExprNode from, ExprNode to, ExprNode step, StmtSeqNode ss)
		{
			this.var = var;
			fromExpr = from;
			toExpr = to;
			stepExpr = step;
			stmts = ss;
		}

		public override void Semant(Environ e)
		{
			var.Semant(e);
			Type ty = var.sem_type;

			fromExpr.Semant(e);
			fromExpr = fromExpr.CastTo(ty, e);
			
			toExpr.Semant(e);
			toExpr = toExpr.CastTo(ty, e);
			
			stepExpr.Semant(e);
			stepExpr = stepExpr.CastTo(ty, e);

			stmts.Semant(e);
		}

		public override IEnumerable<string> WriteData()
		{
			string varIdent = var.JoinedWriteData();
			yield return $"for({var.sem_type.Name} {varIdent} = {fromExpr.JoinedWriteData()}; {varIdent}<={toExpr.JoinedWriteData()}; {varIdent}+={stepExpr.JoinedWriteData()})";
			yield return "{";
			foreach(string s in stmts.WriteData())
			{
				yield return s;
			}
			yield return "}";
		}
	}

	///////////////////////////////
	// For each object of a type //
	///////////////////////////////
	public class ForEachNode:StmtNode
	{
		private readonly VarNode var;
		private readonly string typeIdent;
		private readonly StmtSeqNode stmts;
		private Type sem_type;

		public ForEachNode(VarNode v, string t, StmtSeqNode s)
		{
			var = v;
			typeIdent = t;
			stmts = s;
		}

		public override void Semant(Environ e)
		{
			var.Semant(e);
			Type ty = var.sem_type;

			sem_type = e.findType(typeIdent);
			if(sem_type != ty) throw new Ex("Type mismatch");

			stmts.Semant(e);
		}
		//public override void Translate(Codegen g)
		//{
		//	TNode t, l, r;
		//	string _loop = genLabel();

		//	string objFirst,objNext;

		//	if(var.isObjParam())
		//	{
		//		objFirst = "__bbObjEachFirst2";
		//		objNext = "__bbObjEachNext2";
		//	}
		//	else
		//	{
		//		objFirst = "__bbObjEachFirst";
		//		objNext = "__bbObjEachNext";
		//	}

		//	l = var.Translate(g);
		//	r = global("_t" + typeIdent);
		//	t = jumpf(call(objFirst, l, r), sem_brk);
		//	g.code(t);

		//	g.label(_loop);
		//	stmts.Translate(g);

		//	t = jumpt(call(objNext, var.Translate(g)), _loop);
		//	g.code(t);

		//	g.label(sem_brk);
		//}

		public override IEnumerable<string> WriteData()
		{
			yield return $"foreach(var {var.JoinedWriteData()} in Blitz.AllObjects<{sem_type.Name}>())";
			yield return "{";
			foreach(string s in stmts.WriteData())
			{
				yield return s;
			}
			yield return "}";
		}
	}

	////////////////////////////
	// Return from a function //
	////////////////////////////
	public class ReturnNode:StmtNode
	{
		private ExprNode expr;

		public ReturnNode(ExprNode e)
		{
			expr = e;
		}

		public override void Semant(Environ e)
		{
			if(e.level <= 0 && expr!=null)
			{
				throw new Ex("Main program cannot return a value");
			}
			if(e.level > 0)
			{
				if(expr is null)
				{
					if(e.returnType == Type.Float)
					{
						expr = new FloatConstNode("0");
					}
					else if(e.returnType == Type.String)
					{
						expr = new StringConstNode("\"\"");
					}
					else if(e.returnType is StructType)
					{
						expr = new NullNode();
					}
					else
					{
						expr = new IntConstNode("0");
					}
				}
				expr.Semant(e);
				expr = expr.CastTo(e.returnType, e);
				//returnLabel = e.funcLabel + "_leave";
			}
		}

		public override IEnumerable<string> WriteData()
		{
			if(expr is null)
			{
				yield return "return;";
			}
			else
			{
				yield return $"return {expr.JoinedWriteData()};";
			}
		}
	}

	//////////////////////
	// Delete statement //
	//////////////////////
	public class DeleteNode:StmtNode
	{
		private ExprNode expr;
		public DeleteNode(ExprNode e)
		{
			expr = e;
		}

		public override void Semant(Environ e) => expr.Semant(e);

		public override IEnumerable<string> WriteData()
		{
			yield return $"__bbObjDelete({expr.JoinedWriteData()});";
		}
	}

	///////////////////////////
	// Delete each of a type //
	///////////////////////////
	public class DeleteEachNode:StmtNode
	{
		private readonly string typeIdent;
		private Type sem_type;

		public DeleteEachNode(string t)
		{
			typeIdent=t;
		}
		public override void Semant(Environ e)
		{
			sem_type = e.findType(typeIdent);
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"__bbObjDeleteEach<{sem_type.Name}>();";
		}
	}

	///////////////////////////
	// Insert object in list //
	///////////////////////////
	public class InsertNode:StmtNode
	{
		private ExprNode expr1, expr2;
		private readonly bool before;
		public InsertNode(ExprNode e1, ExprNode e2, bool b)
		{
			expr1 = e1;
			expr2 = e2;
			before = b;
		}

		public override void Semant(Environ e)
		{
			expr1.Semant(e);
			expr2.Semant(e);
			if(!(expr1.Sem_Type is StructType && expr2.Sem_Type is StructType))
			{
				throw new Ex("Illegal expression type");
			}
			if(expr1.Sem_Type != expr2.Sem_Type)
			{
				throw new Ex("Objects types are differnt");
			}
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"{(before ? "__bbObjInsBefore" : "__bbObjInsAfter")}({expr1.JoinedWriteData()}, {expr2.JoinedWriteData()});";
		}
	}

	public class CaseNode:_StmtNode
	{
		public readonly ExprSeqNode exprs;
		public readonly StmtSeqNode stmts;
		public CaseNode(ExprSeqNode e, StmtSeqNode s)
		{
			exprs = e;
			stmts = s;
		}

		public override IEnumerable<string> WriteData()
		{
			foreach(var expr in exprs.exprs)
			{
				yield return $"case {expr.JoinedWriteData()}:";
			}
			yield return "{";
			foreach(string s in stmts.WriteData())
			{
				yield return s;
			}
			yield return "break;";
			yield return "}";
		}
	}

	////////////////////////
	// A select statement //
	////////////////////////
	public class SelectNode:StmtNode
	{
		private ExprNode expr;//Switch on
		public StmtSeqNode defStmts;//Default case
		private readonly List<CaseNode> cases = new List<CaseNode>();

		public SelectNode(ExprNode e)
		{
			expr = e;
			defStmts = null;
		}

		public void Add(CaseNode c) => cases.Add(c);

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			Type ty = expr.Sem_Type;
			if(ty is StructType)
			{
				throw new Ex("Select cannot be used with objects");
			}

			for(int k = 0; k < cases.Count; ++k)
			{
				CaseNode c = cases[k];
				c.exprs.Semant(e);
				c.exprs.CastTo(ty, e);
				c.stmts.Semant(e);
			}
			defStmts?.Semant(e);
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"switch({expr.JoinedWriteData()})";
			yield return "{";
			foreach(var c in cases)
			{
				foreach(string s in c.WriteData())
				{
					yield return s;
				}
			}
			if(defStmts!=null)
			{
				yield return "default:";
				foreach(string s in defStmts.WriteData())
				{
					yield return s;
				}
			}
			yield return "}";
		}
	}

	////////////////////////////
	// Repeat...Until/Forever //
	////////////////////////////
	public class RepeatNode:StmtNode
	{
		private readonly StmtSeqNode stmts;
		private ExprNode expr;
		public RepeatNode(StmtSeqNode s, ExprNode e)
		{
			stmts = s;
			expr = e;
		}

		public override void Semant(Environ e)
		{
			stmts.Semant(e);
			if(expr!=null)
			{
				expr.Semant(e);
				expr = expr.CastTo(Type.Int, e);
			}
		}

		public override IEnumerable<string> WriteData()
		{
			yield return "do";
			yield return "{";
			foreach(string s in stmts.WriteData())
			{
				yield return s;
			}
			yield return "}";
			yield return $"while(!{expr.JoinedWriteData()});";
		}
	}

	///////////////
	// Read data //
	///////////////
	public class ReadNode:StmtNode
	{
		private readonly VarNode var;
		public ReadNode(VarNode v)
		{
			var = v;
		}

		public override void Semant(Environ e) => var.Semant(e);

		public override IEnumerable<string> WriteData()
		{
			yield return $"{var.JoinedWriteData()} = BlitzData.Current.Read<{var.sem_type.Name}>();";
		}
	}

	//////////////////
	// Restore data //
	//////////////////
	public class RestoreNode:StmtNode
	{
		private readonly string ident;
		private Label sem_label;

		public RestoreNode(string i)
		{
			ident = i;
		}
		public override void Semant(Environ e)
		{
			if(e.level > 0)
			{
				e = e.globals;
			}
			sem_label = e.GetLabel(ident);
		}

		public override IEnumerable<string> WriteData()
		{
			//__bbRestore
			yield return $"BlitzData.Current = {sem_label.Name};";
		}
	}
}