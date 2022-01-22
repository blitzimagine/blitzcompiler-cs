using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Blitz3D.Converter.Parsing.Nodes
{
	public abstract class StmtNode:Node
	{
		public string Comment;

		public abstract IEnumerable<string> WriteData();
	}

	public class CommentStmtNode:StmtNode
	{
		public override IEnumerable<string> WriteData()
		{
			yield return Comment;
		}
	}

	////////////////////////
	// Statement Sequence //
	////////////////////////
	public class StmtSeqNode:Node
	{
		public string Comment_Start;
		public string Comment_End;

		public string File{get;}
		public List<StmtNode> Stmts{get;} = new List<StmtNode>();
		public StmtSeqNode(string f)
		{
			File = f;
		}

		public override void Semant(Environ e)
		{
			foreach(var stmt in Stmts)
			{
				try
				{
					stmt.Semant(e);
				}
				catch(Ex x)
				{
					x.File ??= File;
					throw;
				}
			}
		}

		public void AddComment(string comment)
		{
			if(string.IsNullOrEmpty(comment)){return;}
			if(Stmts.Count==0 || Stmts[Stmts.Count-1].Comment != null)
			{
				Add(new CommentStmtNode());
			}
			Stmts[Stmts.Count-1].Comment = comment;
		}

		public void Add(StmtNode s) => Stmts.Add(s);

		public int Count => Stmts.Count;

		public IEnumerable<string> WriteData()
		{
			yield return "{"+Comment_Start;
			foreach(var stmt in Stmts)
			{
				foreach(string s in stmt.WriteData())
				{
					yield return s;
				}
			}
			yield return "}"+Comment_End;
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
			yield return $"using static {Path.ChangeExtension(include.FileName,null).Replace('-','_')};{Comment}";
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
		}

		public override void Semant(Environ e)
		{
			decl.Proto(e.Decls, e);
			decl.Semant(e);
		}

		public override IEnumerable<string> WriteData()
		{
			yield return decl.WriteData().Single()+Comment+(decl.Comment??"");
		}
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
			if(e.FindDecl(ident) is Decl d)
			{
				if(!(d.Type is ArrayType a) || a.Rank != exprs.Count || (t!=null && a.ElementType != t))
				{
					throw new Ex("Duplicate identifier");
				}
				sem_type = a;
				sem_decl = null;
			}
			else
			{
				sem_type = new ArrayType(t ?? Type.Int, exprs.Count);
				sem_decl = e.Decls.InsertDecl(ident, sem_type, DeclKind.Array);
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
			yield return $"{sem_type.Name} {ident} = new {sem_type.ElementType.Name}[{exprs.JoinedWriteData()}];{Comment}";
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
			expr.Semant(e);
			expr = expr.CastTo(var.sem_type, e);
		}

		public override IEnumerable<string> WriteData()
		{
			if(var.sem_type is StructType && expr is IntConstNode c && c.Literal=="0")
			{
				yield return $"{var.JoinedWriteData()} = null;{Comment}";
			}
			else
			{
				yield return $"{var.JoinedWriteData()} = {expr.JoinedWriteData()};{Comment}";
			}
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
			yield return $"{expr.JoinedWriteData()};{Comment}";
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
			sem_label = e.DefineLabel(ident);
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"{sem_label.Name}:;{Comment}";
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
			yield return $"goto {sem_label};{Comment}";
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
		private readonly StmtSeqNode stmts;
		private readonly StmtSeqNode elseOpt;

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
			yield return $"if({expr.JoinedWriteData()}){Comment}";
			foreach(string s in stmts.WriteData())
			{
				yield return s;
			}
			if(elseOpt!=null)
			{
				if(elseOpt.Count == 1 && elseOpt.Stmts[0] is IfNode elseIf)
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
					foreach(string s in elseOpt.WriteData())
					{
						yield return s;
					}
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
			yield return $"break;{Comment}";
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
			yield return $"while({expr.JoinedWriteData()}){Comment}";
			foreach(string s in stmts.WriteData())
			{
				yield return s;
			}
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
			string varIdent;
			if(var is DeclVarNode declVarNode)
			{
				varIdent = declVarNode.sem_decl.Name;
			}
			else
			{
				varIdent = var.JoinedWriteData();
			}

			yield return $"for({var.JoinedWriteData()} = {fromExpr.JoinedWriteData()}; {varIdent}<={toExpr.JoinedWriteData()}; {varIdent}+={stepExpr.JoinedWriteData()}){Comment}";
			foreach(string s in stmts.WriteData())
			{
				yield return s;
			}
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

			sem_type = e.FindType(typeIdent);

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
			yield return $"foreach({var.JoinedWriteData()} in Blitz.AllObjects<{sem_type.Name}>()){Comment}";
			foreach(string s in stmts.WriteData())
			{
				yield return s;
			}
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
			if(e.Level <= 0 && expr!=null)
			{
				throw new Ex("Main program cannot return a value");
			}
			if(e.Level > 0)
			{
				if(expr is null)
				{
					if(e.ReturnType == Type.Float)
					{
						expr = new FloatConstNode("0");
					}
					else if(e.ReturnType == Type.String)
					{
						expr = new StringConstNode("\"\"");
					}
					else if(e.ReturnType is StructType)
					{
						expr = new NullNode();
					}
					else
					{
						expr = new IntConstNode("0");
					}
				}
				expr.Semant(e);
				expr = expr.CastTo(e.ReturnType, e);
				//returnLabel = e.funcLabel + "_leave";
			}
		}

		public override IEnumerable<string> WriteData()
		{
			if(expr is null)
			{
				yield return $"return;{Comment}";
			}
			else
			{
				yield return $"return {expr.JoinedWriteData()};{Comment}";
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
			yield return $"__bbObjDelete({expr.JoinedWriteData()});{Comment}";
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
			sem_type = e.FindType(typeIdent);
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"__bbObjDeleteEach<{sem_type.Name}>();{Comment}";
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
			yield return $"{(before ? "__bbObjInsBefore" : "__bbObjInsAfter")}({expr1.JoinedWriteData()}, {expr2.JoinedWriteData()});{Comment}";
		}
	}

	public class CaseNode:StmtNode
	{
		public ExprSeqNode Exprs{get;}
		public StmtSeqNode Stmts{get;}
		public CaseNode(ExprSeqNode e, StmtSeqNode s)
		{
			Exprs = e;
			Stmts = s;
		}

		public override IEnumerable<string> WriteData()
		{
			foreach(var expr in Exprs.Exprs)
			{
				yield return $"case {expr.JoinedWriteData()}:{Comment}";
			}
			foreach(string s in Stmts.WriteData())
			{
				yield return s;
			}
			yield return "break;";
		}
	}

	public class DefaultCaseNode:CaseNode
	{
		public DefaultCaseNode(StmtSeqNode s):base(null, s){}

		public override IEnumerable<string> WriteData()
		{
			yield return $"default:{Comment}";
			foreach(string s in Stmts.WriteData())
			{
				yield return s;
			}
			yield return "break;";
		}
	}

	////////////////////////
	// A select statement //
	////////////////////////
	public class SelectNode:StmtNode
	{
		private ExprNode expr;//Switch on
		//public StmtSeqNode defStmts;//Default case
		//private readonly List<CaseNode> cases = new List<CaseNode>();
		private readonly StmtSeqNode cases = new StmtSeqNode(null);

		public SelectNode(ExprNode e)
		{
			expr = e;
		}

		public void Add(CaseNode c) => cases.Add(c);

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			Type ty = expr.Sem_Type;

			for(int k = 0; k < cases.Count; ++k)
			{
				CaseNode c = (CaseNode)cases.Stmts[k];
				if(c.Exprs!=null)
				{
					c.Exprs.Semant(e);
					c.Exprs.CastTo(ty, e);
				}
				c.Stmts.Semant(e);
			}
			//defStmts?.Semant(e);
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"switch({expr.JoinedWriteData()}){Comment}";
			foreach(string s in cases.WriteData())
			{
				yield return s;
			}
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
			else
			{

			}
		}

		public override IEnumerable<string> WriteData()
		{
			if(expr!=null)
			{
				yield return $"do{Comment}";
			}
			else
			{
				yield return $"while(true){Comment}";
			}
			foreach(string s in stmts.WriteData())
			{
				yield return s;
			}
			if(expr!=null)
			{
				yield return $"while(!{expr.JoinedWriteData()});";
			}
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
			yield return $"{var.JoinedWriteData()} = BlitzData.Current.Read<{var.sem_type.Name}>();{Comment}";
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
			if(e.Level > 0)
			{
				e = e.Parent;
			}
			sem_label = e.GetLabel(ident);
		}

		public override IEnumerable<string> WriteData()
		{
			//__bbRestore
			yield return $"BlitzData.Current = {sem_label.Name};{Comment}";
		}
	}
}