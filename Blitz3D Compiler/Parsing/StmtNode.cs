using System.Collections.Generic;
using Blitz3D.Compiling;

namespace Blitz3D.Parsing
{
	public abstract class _StmtNode:Node
	{
		protected static string fileLabel;
		protected static Dictionary<string,string> fileMap = new Dictionary<string, string>();
	}

	public abstract class StmtNode:_StmtNode
	{
		public int pos = -1; //offset in source stream

		public abstract void Semant(Environ e);
		public abstract void Translate(Codegen g);
	}

	////////////////////////
	// Statement Sequence //
	////////////////////////
	public class StmtSeqNode:_StmtNode
	{
		public string file;
		private List<StmtNode> stmts = new List<StmtNode>();
		public StmtSeqNode(string f) { file = f; }

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
					if(x.pos < 0) x.pos = stmts[k].pos;
					if(x.file.Length==0) x.file = file;
					throw;
				}
			}
		}
		public void Translate(Codegen g)
		{
			string t = fileLabel;
			fileLabel = file.Length>0 ? fileMap[file] : "";
			for(int k = 0; k < stmts.Count; ++k)
			{
				StmtNode stmt = stmts[k];
				try
				{
					stmt.Translate(g);
				}
				catch(Ex x)
				{
					if(x.pos < 0) x.pos = stmts[k].pos;
					if(x.file.Length==0) x.file = file;
					throw;
				}
			}
			fileLabel = t;
		}

		public void Add(StmtNode s) => stmts.Add(s);

		public int Count => stmts.Count;

		public static void Reset(string file, string lab)
		{
			fileLabel = "";
			fileMap.Clear();

			fileMap[file] = lab;
		}
	}

	//#include "exprnode.h"
	//#include "declnode.h"

	/////////////////
	// An Include! //
	/////////////////
	public class IncludeNode:StmtNode
	{
		private string file,label;
		private StmtSeqNode stmts;
		public IncludeNode(string t, StmtSeqNode ss)
		{
			file = t;
			stmts = ss;
		}

		public override void Semant(Environ e)
		{
			label = genLabel();
			fileMap[file] = label;

			stmts.Semant(e);
		}
		public override void Translate(Codegen g)
		{
			stmts.Translate(g);
		}
	}

	///////////////////
	// a declaration //
	///////////////////
	public class DeclStmtNode:StmtNode
	{
		private DeclNode decl;

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
		public override void Translate(Codegen g)
		{
			decl.Translate(g);
		}
	}

	//////////////////////////////
	// Dim AND declare an Array //
	//////////////////////////////
	public class DimNode:StmtNode
	{
		private string ident,tag;
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
				ArrayType a = d.type.arrayType();
				if(a is null || a.dims != exprs.Count || (t!=null && a.elementType != t))
				{
					ex("Duplicate identifier");
				}
				sem_type = a;
				sem_decl = null;
			}
			else
			{
				if(e.level > 0) ex("Array not found in main program");
				if(t is null) t = Type.int_type;
				sem_type = new ArrayType(t, exprs.Count);
				sem_decl = e.decls.insertDecl(ident, sem_type, DECL.ARRAY);
				e.types.Add(sem_type);
			}
			exprs.semant(e);
			exprs.castTo(Type.int_type, e);
		}
		public override void Translate(Codegen g)
		{
			g.code(call("__bbUndimArray", global("_a" + ident)));
			for(int k = 0; k < exprs.Count; ++k)
			{
				TNode t = add(global("_a" + ident), iconst(k * 4 + 12));
				t = move(exprs.exprs[k].Translate(g), mem(t));
				g.code(t);
			}
			g.code(call("__bbDimArray", global("_a" + ident)));

			if(sem_decl is null) return;

			int et;
			Type ty = sem_type.arrayType().elementType;
			if(ty == Type.int_type) et = 1;
			else if(ty == Type.float_type) et = 2;
			else if(ty == Type.string_type) et = 3;
			else et = 5;

			g.align_data(4);
			g.i_data(0, "_a" + ident);
			g.i_data(et);
			g.i_data(exprs.Count);
			for(int k = 0; k < exprs.Count; ++k) g.i_data(0);
		}
	}

	////////////////
	// Assignment //
	////////////////
	public class AssNode:StmtNode
	{
		private VarNode var;
		private ExprNode expr;
		public AssNode(VarNode var, ExprNode expr)
		{
			this.var = var;
			this.expr = expr;
		}

		public override void Semant(Environ e)
		{
			var.semant(e);
			if(var.sem_type.constType()!=null) ex("Constants can not be assigned to");
			if(var.sem_type.vectorType()!=null) ex("Blitz arrays can not be assigned to");
			expr = expr.Semant(e);
			expr = expr.castTo(var.sem_type, e);
		}
		public override void Translate(Codegen g)
		{
			g.code(var.store(g, expr.Translate(g)));
		}
	}

	//////////////////////////
	// Expression statement //
	//////////////////////////
	public class ExprStmtNode:StmtNode
	{
		private ExprNode expr;
		public ExprStmtNode(ExprNode e)
		{
			expr = e;
		}

		public override void Semant(Environ e)
		{
			expr = expr.Semant(e);
		}
		public override void Translate(Codegen g)
		{
			TNode t = expr.Translate(g);
			if(expr.sem_type == Type.string_type) t = call("__bbStrRelease", t);
			g.code(t);
		}
	}

	////////////////
	// user label //
	////////////////
	public class LabelNode:StmtNode
	{
		private string ident;
		private int data_sz;
		public LabelNode(string s, int sz)
		{
			ident = s;
			data_sz = sz;
		}
		public override void Semant(Environ e)
		{
			if(e.findLabel(ident) is Label l)
			{
				if(l.def >= 0) ex("duplicate label");
				l.def = pos;
				l.data_sz = data_sz;
			}
			else e.insertLabel(ident, pos, -1, data_sz);
			ident = e.funcLabel + ident;
		}
		public override void Translate(Codegen g)
		{
			g.label("_l" + ident);
		}
	}

	////////////////////
	// Goto statement //
	////////////////////
	public class GotoNode:StmtNode
	{
		private string ident;
		public GotoNode(string s) { ident=s; }
		public override void Semant(Environ e)
		{
			if(e.findLabel(ident) is null)
			{
				e.insertLabel(ident, -1, pos, -1);
			}
			ident = e.funcLabel + ident;
		}
		public override void Translate(Codegen g)
		{
			g.code(jump("_l" + ident));
		}
	}

	/////////////////////
	// Gosub statement //
	/////////////////////
	public class GosubNode:StmtNode
	{
		private string ident;
		public GosubNode(string s) { ident = s; }
		public override void Semant(Environ e)
		{
			if(e.level > 0) ex("'Gosub' may not be used inside a function");
			if(e.findLabel(ident) is null) e.insertLabel(ident, -1, pos, -1);
			ident = e.funcLabel + ident;
		}
		public override void Translate(Codegen g)
		{
			g.code(jsr("_l" + ident));
		}
	}

	//////////////////
	// If statement //
	//////////////////
	public class IfNode:StmtNode
	{
		private ExprNode expr;
		private StmtSeqNode stmts,elseOpt;
		public IfNode(ExprNode e, StmtSeqNode s, StmtSeqNode o)
		{
			expr=e;
			stmts=s;
			elseOpt=o;
		}

		public override void Semant(Environ e)
		{
			expr = expr.Semant(e);
			expr = expr.castTo(Type.int_type, e);
			stmts.Semant(e);
			if(elseOpt!=null) elseOpt.Semant(e);
		}
		public override void Translate(Codegen g)
		{
			if(expr.constNode() is ConstNode c)
			{
				if(c.intValue()!=0) stmts.Translate(g);
				else if(elseOpt!=null) elseOpt.Translate(g);
			}
			else
			{
				string _else = genLabel();
				g.code(jumpf(expr.Translate(g), _else));
				stmts.Translate(g);
				if(elseOpt!=null)
				{
					string _else2 = genLabel();
					g.code(jump(_else2));
					g.label(_else);
					elseOpt.Translate(g);
					_else = _else2;
				}
				g.label(_else);
			}
		}
	}

	///////////
	// Break //
	///////////
	public class ExitNode:StmtNode
	{
		private string sem_brk;
		public override void Semant(Environ e)
		{
			sem_brk = e.breakLabel;
			if(sem_brk.Length==0) ex("break must appear inside a loop");
		}
		public override void Translate(Codegen g)
		{
			g.code(new TNode(IR.JUMP, null, null, sem_brk));
		}
	}

	/////////////////////
	// While statement //
	/////////////////////
	public class WhileNode:StmtNode
	{
		private int wendPos;
		private ExprNode expr;
		private StmtSeqNode stmts;
		private string sem_brk;
		public WhileNode(ExprNode e, StmtSeqNode s, int wp)
		{
			wendPos = wp;
			expr = e;
			stmts = s;
		}

		public override void Semant(Environ e)
		{
			expr = expr.Semant(e);
			expr = expr.castTo(Type.int_type, e);
			string brk = e.setBreak(sem_brk = genLabel());
			stmts.Semant(e);
			e.setBreak(brk);
		}
		public override void Translate(Codegen g)
		{
			string loop = genLabel();
			if(expr.constNode() is ConstNode c)
			{
				if(c.intValue() == 0) return;
				g.label(loop);
				stmts.Translate(g);
				g.code(jump(loop));
			}
			else
			{
				string cond = genLabel();
				g.code(jump(cond));
				g.label(loop);
				stmts.Translate(g);
				g.label(cond);
				g.code(jumpt(expr.Translate(g), loop));
			}
			g.label(sem_brk);
		}
	}

	///////////////////
	// For/Next loop //
	///////////////////
	public class ForNode:StmtNode
	{
		private int nextPos;
		private VarNode var;
		private ExprNode fromExpr,toExpr,stepExpr;
		private StmtSeqNode stmts;
		private string sem_brk;
		public ForNode(VarNode var, ExprNode from, ExprNode to, ExprNode step, StmtSeqNode ss, int np)
		{
			nextPos = np;
			this.var = var;
			fromExpr = from;
			toExpr = to;
			stepExpr = step;
			stmts = ss;
		}

		public override void Semant(Environ e)
		{
			var.semant(e);
			Type ty = var.sem_type;
			if(ty.constType()!=null) ex("Index variable can not be constant");
			if(ty != Type.int_type && ty != Type.float_type)
			{
				ex("index variable must be integer or real");
			}
			fromExpr = fromExpr.Semant(e);
			fromExpr = fromExpr.castTo(ty, e);
			toExpr = toExpr.Semant(e);
			toExpr = toExpr.castTo(ty, e);
			stepExpr = stepExpr.Semant(e);
			stepExpr = stepExpr.castTo(ty, e);

			if(stepExpr.constNode() is null) ex("Step value must be constant");

			string brk = e.setBreak(sem_brk = genLabel());
			stmts.Semant(e);
			e.setBreak(brk);
		}

		public override void Translate(Codegen g)
		{
			TNode t;
			Type ty = var.sem_type;

			//initial assignment
			g.code(var.store(g, fromExpr.Translate(g)));

			string cond = genLabel();
			string loop = genLabel();
			g.code(jump(cond));
			g.label(loop);
			stmts.Translate(g);

			//execute the step part
			IR op = ty == Type.int_type ? IR.ADD : IR.FADD;
			t = new TNode(op, var.load(g), stepExpr.Translate(g));
			g.code(var.store(g, t));

			//test for loop cond
			g.label(cond);
			Keyword kw = stepExpr.constNode().floatValue() > 0 ? Keyword.GT : Keyword.LT;
			t = compare(kw, var.load(g), toExpr.Translate(g), ty);
			g.code(jumpf(t, loop));

			g.label(sem_brk);
		}
	}

	///////////////////////////////
	// For each object of a type //
	///////////////////////////////
	public class ForEachNode:StmtNode
	{
		private int nextPos;
		private VarNode var;
		private string typeIdent;
		private StmtSeqNode stmts;
		private string sem_brk;
		public ForEachNode(VarNode v, string t, StmtSeqNode s, int np)
		{
			nextPos = np;
			var = v;
			typeIdent = t;
			stmts = s;
		}

		public override void Semant(Environ e)
		{
			var.semant(e);
			Type ty = var.sem_type;

			if(ty.structType() == null) ex("Index variable is not a NewType");
			Type t = e.findType(typeIdent);
			if(t is null) ex("Type name not found");
			if(t != ty) ex("Type mismatch");

			string brk = e.setBreak(sem_brk = genLabel());
			stmts.Semant(e);
			e.setBreak(brk);
		}
		public override void Translate(Codegen g)
		{
			TNode t, l, r;
			string _loop = genLabel();

			string objFirst,objNext;

			if(var.isObjParam())
			{
				objFirst = "__bbObjEachFirst2";
				objNext = "__bbObjEachNext2";
			}
			else
			{
				objFirst = "__bbObjEachFirst";
				objNext = "__bbObjEachNext";
			}

			l = var.translate(g);
			r = global("_t" + typeIdent);
			t = jumpf(call(objFirst, l, r), sem_brk);
			g.code(t);

			g.label(_loop);
			stmts.Translate(g);

			t = jumpt(call(objNext, var.translate(g)), _loop);
			g.code(t);

			g.label(sem_brk);
		}
	}

	////////////////////////////
	// Return from a function //
	////////////////////////////
	public class ReturnNode:StmtNode
	{
		private ExprNode expr;
		private string returnLabel;
		public ReturnNode(ExprNode e)
		{
			expr = e;
		}

		public override void Semant(Environ e)
		{
			if(e.level <= 0 && expr!=null)
			{
				ex("Main program cannot return a value");
			}
			if(e.level > 0)
			{
				if(expr is null)
				{
					if(e.returnType == Type.float_type)
					{
						expr = new FloatConstNode(0);
					}
					else if(e.returnType == Type.string_type)
					{
						expr = new StringConstNode("");
					}
					else if(e.returnType.structType()!=null)
					{
						expr = new NullNode();
					}
					else
					{
						expr = new IntConstNode(0);
					}
				}
				expr = expr.Semant(e);
				expr = expr.castTo(e.returnType, e);
				returnLabel = e.funcLabel + "_leave";
			}
		}
		public override void Translate(Codegen g)
		{
			if(expr is null)
			{
				g.code(new TNode(IR.RET, null, null));
				return;
			}

			TNode t = expr.Translate(g);

			if(expr.sem_type == Type.float_type)
			{
				g.code(new TNode(IR.FRETURN, t, null, returnLabel));
			}
			else
			{
				g.code(new TNode(IR.RETURN, t, null, returnLabel));
			}
		}
	}

	//////////////////////
	// Delete statement //
	//////////////////////
	public class DeleteNode:StmtNode
	{
		private ExprNode expr;
		public DeleteNode(ExprNode e) { expr = e; }

		public override void Semant(Environ e)
		{
			expr = expr.Semant(e);
			if(expr.sem_type.structType() == null)
			{
				ex("Can't delete non-Newtype");
			}
		}
		public override void Translate(Codegen g)
		{
			TNode t = expr.Translate(g);
			g.code(call("__bbObjDelete", t));
		}
	}

	///////////////////////////
	// Delete each of a type //
	///////////////////////////
	public class DeleteEachNode:StmtNode
	{
		private string typeIdent;
		public DeleteEachNode(string t) { typeIdent=t; }
		public override void Semant(Environ e)
		{
			Type t = e.findType(typeIdent);
			if(t is null || t.structType() == null) ex("Specified name is not a NewType name");
		}
		public override void Translate(Codegen g)
		{
			g.code(call("__bbObjDeleteEach", global("_t" + typeIdent)));
		}
	}

	///////////////////////////
	// Insert object in list //
	///////////////////////////
	public class InsertNode:StmtNode
	{
		private ExprNode expr1, expr2;
		private bool before;
		public InsertNode(ExprNode e1, ExprNode e2, bool b)
		{
			expr1 = e1;
			expr2 = e2;
			before = b;
		}

		public override void Semant(Environ e)
		{
			expr1 = expr1.Semant(e);
			expr2 = expr2.Semant(e);
			StructType t1 = expr1.sem_type.structType();
			StructType t2 = expr2.sem_type.structType();
			if(t1 is null || t2 is null) ex("Illegal expression type");
			if(t1 != t2) ex("Objects types are differnt");
		}
		public override void Translate(Codegen g)
		{
			TNode t1 = expr1.Translate(g);
			TNode t2 = expr2.Translate(g);
			string s = before ? "__bbObjInsBefore" : "__bbObjInsAfter";
			g.code(call(s, t1, t2));
		}
	}

	public class CaseNode:_StmtNode
	{
		public ExprSeqNode exprs;
		public StmtSeqNode stmts;
		public CaseNode(ExprSeqNode e, StmtSeqNode s)
		{
			exprs = e;
			stmts = s;
		}
	}

	////////////////////////
	// A select statement //
	////////////////////////
	public class SelectNode:StmtNode
	{
		private ExprNode expr;
		public StmtSeqNode defStmts;
		private List<CaseNode> cases = new List<CaseNode>();
		private VarNode sem_temp;
		public SelectNode(ExprNode e)
		{
			expr = e;
			defStmts = null;
			sem_temp = null;
		}

		public void Add(CaseNode c) => cases.Add(c);

		public override void Semant(Environ e)
		{
			expr = expr.Semant(e);
			Type ty = expr.sem_type;
			if(ty.structType()!=null) ex("Select cannot be used with objects");

			//we need a temp var
			Decl d = e.decls.insertDecl(genLabel(),expr.sem_type,DECL.LOCAL);
			sem_temp = new DeclVarNode(d);

			for(int k = 0; k < cases.Count; ++k)
			{
				CaseNode c = cases[k];
				c.exprs.semant(e);
				c.exprs.castTo(ty, e);
				c.stmts.Semant(e);
			}
			if(defStmts!=null) defStmts.Semant(e);
		}
		public override void Translate(Codegen g)
		{
			Type ty = expr.sem_type;

			g.code(sem_temp.store(g, expr.Translate(g)));

			List<string> labs = new List<string>();
			string brk = genLabel();

			for(int k = 0; k < cases.Count; ++k)
			{
				CaseNode c = cases[k];
				labs.Add(genLabel());
				for(int j = 0; j < c.exprs.Count; ++j)
				{
					ExprNode e = c.exprs.exprs[j];
					TNode t = compare(Keyword.EQ,sem_temp.load(g),e.Translate(g),ty);
					g.code(jumpt(t, labs[labs.Count-1]));
				}
			}
			if(defStmts!=null) defStmts.Translate(g);
			g.code(jump(brk));
			for(int k = 0; k < (int)cases.Count; ++k)
			{
				CaseNode c = cases[k];
				g.label(labs[k]);
				c.stmts.Translate(g);
				g.code(jump(brk));
			}

			g.label(brk);
		}
	}

	////////////////////////////
	// Repeat...Until/Forever //
	////////////////////////////
	public class RepeatNode:StmtNode
	{
		private int untilPos;
		private StmtSeqNode stmts;
		private ExprNode expr;
		private string sem_brk;
		public RepeatNode(StmtSeqNode s, ExprNode e, int up)
		{
			untilPos = up;
			stmts = s;
			expr = e;
		}

		public override void Semant(Environ e)
		{
			sem_brk = genLabel();
			string brk = e.setBreak(sem_brk);
			stmts.Semant(e);
			e.setBreak(brk);
			if(expr!=null)
			{
				expr = expr.Semant(e);
				expr = expr.castTo(Type.int_type, e);
			}
		}
		public override void Translate(Codegen g)
		{
			string loop = genLabel();
			g.label(loop);
			stmts.Translate(g);

			if(expr?.constNode() is ConstNode c)
			{
				if(c.intValue()==0) g.code(jump(loop));
			}
			else
			{
				if(expr!=null) g.code(jumpf(expr.Translate(g), loop));
				else g.code(jump(loop));
			}
			g.label(sem_brk);
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

		public override void Semant(Environ e)
		{
			var.semant(e);
			if(var.sem_type.constType()!=null) ex("Constants can not be modified");
			if(var.sem_type.structType()!=null) ex("Data can not be read into an object");
		}
		public override void Translate(Codegen g)
		{
			TNode t;
			if(var.sem_type == Type.int_type) t = call("__bbReadInt");
			else if(var.sem_type == Type.float_type) t = fcall("__bbReadFloat");
			else t = call("__bbReadStr");
			g.code(var.store(g, t));
		}
	}

	//////////////////
	// Restore data //
	//////////////////
	public class RestoreNode:StmtNode
	{
		private string ident;
		private Label sem_label;
		public RestoreNode(string i)
		{
			ident = i;
		}
		public override void Semant(Environ e)
		{
			if(e.level > 0) e = e.globals;

			if(ident.Length==0) sem_label = null;
			else
			{
				sem_label = e.findLabel(ident);
				if(sem_label is null) sem_label = e.insertLabel(ident, -1, pos, -1);
			}
		}
		public override void Translate(Codegen g)
		{
			TNode t = global("__DATA");
			if(sem_label != null) t = add(t, iconst(sem_label.data_sz * 8));
			g.code(call("__bbRestore", t));
		}
	}
}