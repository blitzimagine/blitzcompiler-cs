/*

  The parser builds an abstact syntax tree from input tokens.

*/
using System.Collections.Generic;
using System.IO;

public class Parser
{
	private enum STMTS
	{
		PROG,
		BLOCK,
		LINE
	}

	private static bool isTerm(Keyword c) => c == (Keyword)':' || c == (Keyword)'\n';

	public Parser(Toker t)
	{
		toker = t;
	}

	public ProgNode parse(string main)
	{
		incfile = main;

		consts = new DeclSeqNode();
		structs = new DeclSeqNode();
		funcs = new DeclSeqNode();
		datas = new DeclSeqNode();
		try
		{
			StmtSeqNode stmts = parseStmtSeq(STMTS.PROG);
			if(toker.curr != (Keyword)(-1)/*EOF*/) throw exp("end-of-file");
			return new ProgNode(consts, structs, funcs, datas, stmts);
		}
		catch(Ex)
		{
			datas = null;
			funcs = null;
			structs = null;
			consts = null;
			throw;
		}
	}

	private string incfile;
	private HashSet<string> included = new HashSet<string>();
	private Toker toker;
	private Dictionary<string, DimNode> arrayDecls = new Dictionary<string, DimNode>();

	private DeclSeqNode consts;
	private DeclSeqNode structs;
	private DeclSeqNode funcs;
	private DeclSeqNode datas;

	private StmtSeqNode parseStmtSeq(STMTS scope)
	{
		StmtSeqNode stmts = new StmtSeqNode(incfile);//a_ptr<StmtSeqNode>
		parseStmtSeq(stmts, scope);
		return stmts/*.release()*/;
	}
	private void parseStmtSeq(StmtSeqNode stmts, STMTS scope)
	{
		for(;;)
		{
			while(toker.curr == (Keyword)':' || (scope != STMTS.LINE && toker.curr == (Keyword)'\n')) toker.next();
			StmtNode result = null;

			int pos = toker.pos;

			switch(toker.curr)
			{
				case Keyword.INCLUDE:
				{
					if(toker.next() != Keyword.STRINGCONST) throw exp("include filename");
					string inc = toker.text;
					toker.next();
					inc = inc.Substring(1, inc.Length - 2);

					//WIN32 KLUDGE//
					inc = Path.GetFullPath(inc);//getBestPathName
					inc = inc.ToLowerInvariant();

					if(included.Contains(inc)) break;

					StreamReader i_stream = new StreamReader(inc);//ifstream
					//if(!i_stream.good()) throw ex("Unable to open include file");

					Toker i_toker = new Toker(i_stream);

					string t_inc = incfile;
					incfile = inc;
					Toker t_toker = toker;
					toker = i_toker;

					included.Add(incfile);

					StmtSeqNode ss = parseStmtSeq(scope);//a_ptr<StmtSeqNode>
					if(toker.curr != Keyword.EOF) throw exp("end-of-file");

					result = new IncludeNode(incfile, ss/*.release()*/);

					toker = t_toker;
					incfile = t_inc;
				}
				break;
				case Keyword.IDENT:
				{
					string ident = toker.text;
					toker.next();
					string tag = parseTypeTag();
					if(!arrayDecls.ContainsKey(ident) && toker.curr != (Keyword)'=' && toker.curr != (Keyword)'\\' && toker.curr != (Keyword)'[')
					{
						//must be a function
						ExprSeqNode exprs;
						if(toker.curr == (Keyword)'(')
						{
							//ugly lookahead for optional '()' around statement params
							int nest = 1, k;
							for(k = 1; ; ++k)
							{
								Keyword c = toker.lookAhead(k);
								if(isTerm(c)) throw ex("Mismatched brackets");
								else if(c == (Keyword)'(') ++nest;
								else if(c == (Keyword)')' && (--nest)==0) break;
							}
							if(isTerm(toker.lookAhead(++k)))
							{
								toker.next();
								exprs = parseExprSeq();
								if(toker.curr != (Keyword)')') throw exp("')'");
								toker.next();
							}
							else exprs = parseExprSeq();
						}
						else exprs = parseExprSeq();
						CallNode call = new CallNode(ident, tag, exprs);
						result = new ExprStmtNode(call);
					}
					else
					{
						//must be a var
						VarNode var = parseVar(ident, tag);//a_ptr<VarNode>
						if(toker.curr != (Keyword)'=') throw exp("variable assignment");
						toker.next();
						ExprNode expr = parseExpr(false);
						result = new AssNode(var/*.release()*/, expr);
					}
				}
				break;
				case Keyword.IF:
				{
					toker.next();
					result = parseIf();
					if(toker.curr == Keyword.ENDIF) toker.next();
				}
				break;
				case Keyword.WHILE:
				{
					toker.next();
					ExprNode expr = parseExpr(false);//a_ptr<ExprNode>
					StmtSeqNode stmts2 = parseStmtSeq(STMTS.BLOCK);//a_ptr<StmtSeqNode>
					int pos2 = toker.pos;
					if(toker.curr != Keyword.WEND) throw exp("'Wend'");
					toker.next();
					result = new WhileNode(expr/*.release()*/, stmts2/*.release()*/, pos2);
				}
				break;
				case Keyword.REPEAT:
				{
					toker.next();
					ExprNode expr = null;
					StmtSeqNode stmts2 = parseStmtSeq(STMTS.BLOCK);//a_ptr<StmtSeqNode>
					Keyword curr = (Keyword)toker.curr;
					int pos2 = toker.pos;
					if(curr != Keyword.UNTIL && curr != Keyword.FOREVER) throw exp("'Until' or 'Forever'");
					toker.next();
					if(curr == Keyword.UNTIL) expr = parseExpr(false);
					result = new RepeatNode(stmts2/*.release()*/, expr, pos2);
				}
				break;
				case Keyword.SELECT:
				{
					toker.next();
					ExprNode expr = parseExpr(false);
					SelectNode selNode = new SelectNode(expr);//a_ptr<SelectNode>
					for(;;)
					{
						while(isTerm(toker.curr)) toker.next();
						if(toker.curr == Keyword.CASE)
						{
							toker.next();
							ExprSeqNode exprs = parseExprSeq();//a_ptr<ExprSeqNode>
							if(exprs.size()==0) throw exp("expression sequence");
							StmtSeqNode stmts2 = parseStmtSeq(STMTS.BLOCK);//a_ptr<StmtSeqNode>
							selNode.push_back(new CaseNode(exprs/*.release()*/, stmts2/*.release()*/));
							continue;
						}
						if(toker.curr == Keyword.DEFAULT)
						{
							toker.next();
							StmtSeqNode stmts2 = parseStmtSeq(STMTS.BLOCK);//a_ptr<StmtSeqNode>
							if(toker.curr != Keyword.ENDSELECT) throw exp("'End Select'");
							selNode.defStmts = stmts2/*.release()*/;
							break;
						}
						if(toker.curr == Keyword.ENDSELECT)
						{
							break;
						}
						throw exp("'Case', 'Default' or 'End Select'");
					}
					toker.next();
					result = selNode/*.release()*/;
				}
				break;
				case Keyword.FOR:
				{
					VarNode var;//a_ptr<VarNode>
					StmtSeqNode stmts2;//a_ptr<StmtSeqNode>
					toker.next();
					var = parseVar();
					if(toker.curr != (Keyword)'=') throw exp("variable assignment");
					if(toker.next() == Keyword.EACH)
					{
						toker.next();
						string ident = parseIdent();
						stmts2 = parseStmtSeq(STMTS.BLOCK);
						int pos2 = toker.pos;
						if(toker.curr != Keyword.NEXT) throw exp("'Next'");
						toker.next();
						result = new ForEachNode(var/*.release()*/, ident, stmts2/*.release()*/, pos2);
					}
					else
					{
						ExprNode from, to, step;//a_ptr<ExprNode>
						from = parseExpr(false);
						if(toker.curr != Keyword.TO) throw exp("'TO'");
						toker.next();
						to = parseExpr(false);
						//step...
						if(toker.curr == Keyword.STEP)
						{
							toker.next();
							step = parseExpr(false);
						}
						else step = new IntConstNode(1);
						stmts2 = parseStmtSeq(STMTS.BLOCK);
						int pos2 = toker.pos;
						if(toker.curr != Keyword.NEXT) throw exp("'Next'");
						toker.next();
						result = new ForNode(var/*.release()*/, from/*.release()*/, to/*.release()*/, step/*.release()*/, stmts2/*.release()*/, pos2);
					}
				}
				break;
				case Keyword.EXIT:
				{
					toker.next();
					result = new ExitNode();
				}
				break;
				case Keyword.GOTO:
				{
					toker.next();
					string t = parseIdent();
					result = new GotoNode(t);
				}
				break;
				case Keyword.GOSUB:
				{
					toker.next();
					string t = parseIdent();
					result = new GosubNode(t);
				}
				break;
				case Keyword.RETURN:
				{
					toker.next();
					result = new ReturnNode(parseExpr(true));
				}
				break;
				case Keyword.BBDELETE:
				{
					if(toker.next() == Keyword.EACH)
					{
						toker.next();
						string t = parseIdent();
						result = new DeleteEachNode(t);
					}
					else
					{
						ExprNode expr = parseExpr(false);
						result = new DeleteNode(expr);
					}
				}
				break;
				case Keyword.INSERT:
				{
					toker.next();
					ExprNode expr1 = parseExpr(false);//a_ptr<ExprNode>
					if(toker.curr != Keyword.BEFORE && toker.curr != Keyword.AFTER) throw exp("'Before' or 'After'");
					bool before = toker.curr == Keyword.BEFORE;
					toker.next();
					ExprNode expr2 = parseExpr(false);//a_ptr<ExprNode>
					result = new InsertNode(expr1/*.release()*/, expr2/*.release()*/, before);
				}
				break;
				case Keyword.READ:
					do
					{
						toker.next();
						VarNode var = parseVar();
						StmtNode stmt = new ReadNode(var);
						stmt.pos = pos;
						pos = toker.pos;
						stmts.push_back(stmt);
					} while(toker.curr == (Keyword)',');
					break;
				case Keyword.RESTORE:
					if(toker.next() == Keyword.IDENT)
					{
						result = new RestoreNode(toker.text);
						toker.next();
					}
					else result = new RestoreNode("");
					break;
				case Keyword.DATA:
					if(scope != STMTS.PROG) throw ex("'Data' can only appear in main program");
					do
					{
						toker.next();
						ExprNode expr = parseExpr(false);
						datas.push_back(new DataDeclNode(expr));
					} while(toker.curr == (Keyword)',');
					break;
				case Keyword.TYPE:
					if(scope != STMTS.PROG) throw ex("'Type' can only appear in main program");
					toker.next();
					structs.push_back(parseStructDecl());
					break;
				case Keyword.BBCONST:
					if(scope != STMTS.PROG) throw ex("'Const' can only appear in main program");
					do
					{
						toker.next();
						consts.push_back(parseVarDecl(DECL.GLOBAL, true));
					} while(toker.curr == (Keyword)',');
					break;
				case Keyword.FUNCTION:
					if(scope != STMTS.PROG) throw ex("'Function' can only appear in main program");
					toker.next();
					funcs.push_back(parseFuncDecl());
					break;
				case Keyword.DIM:
					do
					{
						toker.next();
						StmtNode stmt = parseArrayDecl();
						stmt.pos = pos;
						pos = toker.pos;
						stmts.push_back(stmt);
					} while(toker.curr == (Keyword)',');
					break;
				case Keyword.LOCAL:
					do
					{
						toker.next();
						DeclNode d = parseVarDecl(DECL.LOCAL, false);
						StmtNode stmt = new DeclStmtNode(d);
						stmt.pos = pos;
						pos = toker.pos;
						stmts.push_back(stmt);
					} while(toker.curr == (Keyword)',');
					break;
				case Keyword.GLOBAL:
					if(scope != STMTS.PROG) throw ex("'Global' can only appear in main program");
					do
					{
						toker.next();
						DeclNode d = parseVarDecl(DECL.GLOBAL, false);
						StmtNode stmt = new DeclStmtNode(d);
						stmt.pos = pos;
						pos = toker.pos;
						stmts.push_back(stmt);
					} while(toker.curr == (Keyword)',');
					break;
				case (Keyword)'.':
				{
					toker.next();
					string t = parseIdent();
					result = new LabelNode(t, datas.size());
				}
				break;
				default:
					return;
			}

			if(result!=null)
			{
				result.pos = pos;
				stmts.push_back(result);
			}
		}
	}

	private Ex ex(string s) => new Ex(s, toker.pos, incfile);
	private Ex exp(string s) => toker.curr switch
	{
		Keyword.NEXT => ex("'Next' without 'For'"),
		Keyword.WEND => ex("'Wend' without 'While'"),
		Keyword.ELSE => ex("'Else' without 'If'"),
		Keyword.ELSEIF => ex("'Elseif' without 'If'"),
		Keyword.ENDIF => ex("'Endif' without 'If'"),
		Keyword.ENDFUNCTION => ex("'End Function' without 'Function'"),
		Keyword.UNTIL => ex("'Until' without 'Repeat'"),
		Keyword.FOREVER => ex("'Forever' without 'Repeat'"),
		Keyword.CASE => ex("'Case' without 'Select'"),
		Keyword.ENDSELECT => ex("'End Select' without 'Select'"),
		_ => ex("Expecting " + s),
	};

	private string parseIdent()
	{
		if(toker.curr != Keyword.IDENT) throw exp("identifier");
		string t = toker.text;
		toker.next();
		return t;
	}
	//private void parseChar(int c)
	//{
	//	if(toker.curr != c) throw exp($"'{c}'");
	//	toker.next();
	//}
	private string parseTypeTag()
	{
		switch(toker.curr)
		{
			case (Keyword)'%':
				toker.next();
				return "%";
			case (Keyword)'#':
				toker.next();
				return "#";
			case (Keyword)'$':
				toker.next();
				return "$";
			case (Keyword)'.':
				toker.next();
				return parseIdent();
			default:
				return "";
		}
	}

	private VarNode parseVar()
	{
		string ident = parseIdent();
		string tag = parseTypeTag();
		return parseVar(ident, tag);
	}
	private VarNode parseVar(string ident, string tag)
	{
		VarNode var;//a_ptr<VarNode>
		if(toker.curr == (Keyword)'(')
		{
			toker.next();
			ExprSeqNode exprs = parseExprSeq();//a_ptr<ExprSeqNode>
			if(toker.curr != (Keyword)')') throw exp("')'");
			toker.next();
			var = new ArrayVarNode(ident, tag, exprs/*.release()*/);
		}
		else var = new IdentVarNode(ident, tag);

		for(; ; )
		{
			if(toker.curr == (Keyword)'\\')
			{
				toker.next();
				string ident2 = parseIdent();
				string tag2 = parseTypeTag();
				ExprNode expr = new VarExprNode(var/*.release()*/);
				var = new FieldVarNode(expr, ident2, tag2);
			}
			else if(toker.curr == (Keyword)'[')
			{
				toker.next();
				ExprSeqNode exprs = parseExprSeq();//a_ptr<ExprSeqNode>
				if(exprs.exprs.Count != 1 || toker.curr != (Keyword)']') throw exp("']'");
				toker.next();
				ExprNode expr = new VarExprNode(var/*.release()*/);
				var = new VectorVarNode(expr, exprs/*.release()*/);
			}
			else
			{
				break;
			}
		}
		return var/*.release()*/;
	}
	//private CallNode parseCall(string ident, string tag);
	private IfNode parseIf()
	{
		ExprNode expr;//a_ptr<ExprNode>
		StmtSeqNode stmts, elseOpt = null;//a_ptr<StmtSeqNode>

		expr = parseExpr(false);
		if(toker.curr == Keyword.THEN) toker.next();

		bool blkif = isTerm(toker.curr);
		stmts = parseStmtSeq(blkif ? STMTS.BLOCK : STMTS.LINE);

		if(toker.curr == Keyword.ELSEIF)
		{
			int pos = toker.pos;
			toker.next();
			IfNode ifnode = parseIf();
			ifnode.pos = pos;
			elseOpt = new StmtSeqNode(incfile);
			elseOpt.push_back(ifnode);
		}
		else if(toker.curr == Keyword.ELSE)
		{
			toker.next();
			elseOpt = parseStmtSeq(blkif ? STMTS.BLOCK : STMTS.LINE);
		}
		if(blkif)
		{
			if(toker.curr != Keyword.ENDIF) throw exp("'EndIf'");
		}
		else if(toker.curr != (Keyword)'\n') throw exp("end-of-line");

		return new IfNode(expr/*.release()*/, stmts/*.release()*/, elseOpt/*.release()*/);
	}

	private DeclNode parseVarDecl(DECL kind, bool constant)
	{
		int pos = toker.pos;
		string ident = parseIdent();
		string tag = parseTypeTag();
		DeclNode d;
		if(toker.curr == (Keyword)'[')
		{
			if(constant) throw ex("Blitz arrays may not be constant");
			toker.next();
			ExprSeqNode exprs = parseExprSeq();//a_ptr<ExprSeqNode>
			if(exprs.size() != 1 || toker.curr != (Keyword)']') throw exp("']'");
			toker.next();
			d = new VectorDeclNode(ident, tag, exprs/*.release()*/, kind);
		}
		else
		{
			ExprNode expr = null;
			if(toker.curr == (Keyword)'=')
			{
				toker.next();
				expr = parseExpr(false);
			}
			else if(constant) throw ex("Constants must be initialized");
			d = new VarDeclNode(ident, tag, kind, constant, expr);
		}
		d.pos = pos;
		d.file = incfile;
		return d;
	}
	private DimNode parseArrayDecl()
	{
		int pos = toker.pos;
		string ident = parseIdent();
		string tag = parseTypeTag();
		if(toker.curr != (Keyword)'(') throw exp("'('");
		toker.next();
		ExprSeqNode exprs = parseExprSeq();//a_ptr<ExprSeqNode>
		if(toker.curr != (Keyword)')') throw exp("')'");
		if(exprs.size()==0) throw ex("can't have a 0 dimensional array");
		toker.next();
		DimNode d = new DimNode(ident, tag, exprs/*.release()*/);
		arrayDecls[ident] = d;
		d.pos = pos;
		return d;
	}
	private DeclNode parseFuncDecl()
	{
		int pos = toker.pos;
		string ident = parseIdent();
		string tag = parseTypeTag();
		if(toker.curr != (Keyword)'(') throw exp("'('");
		DeclSeqNode @params = new DeclSeqNode();//a_ptr<DeclSeqNode>
		if(toker.next() != (Keyword)')')
		{
			for(; ; )
			{
				@params.push_back(parseVarDecl(DECL.PARAM, false));
				if(toker.curr != (Keyword)',') break;
				toker.next();
			}
			if(toker.curr != (Keyword)')') throw exp("')'");
		}
		toker.next();
		StmtSeqNode stmts = parseStmtSeq(STMTS.BLOCK);//a_ptr<StmtSeqNode>
		if(toker.curr != Keyword.ENDFUNCTION) throw exp("'End Function'");
		StmtNode ret = new ReturnNode(null);
		ret.pos = toker.pos;
		stmts.push_back(ret);
		toker.next();
		DeclNode d = new FuncDeclNode(ident, tag, @params/*.release()*/, stmts/*.release()*/);
		d.pos = pos;
		d.file = incfile;
		return d;
	}
	private DeclNode parseStructDecl()
	{
		int pos = toker.pos;
		string ident = parseIdent();
		while(toker.curr == (Keyword)'\n') toker.next();
		DeclSeqNode fields = new DeclSeqNode();//a_ptr<DeclSeqNode>
		while(toker.curr == Keyword.FIELD)
		{
			do
			{
				toker.next();
				fields.push_back(parseVarDecl(DECL.FIELD, false));
			} while(toker.curr == (Keyword)',');
			while(toker.curr == (Keyword)'\n') toker.next();
		}
		if(toker.curr != Keyword.ENDTYPE) throw exp("'Field' or 'End Type'");
		toker.next();
		DeclNode d = new StructDeclNode(ident, fields/*.release()*/);
		d.pos = pos;
		d.file = incfile;
		return d;
	}

	private ExprSeqNode parseExprSeq()
	{
		ExprSeqNode exprs = new ExprSeqNode();//a_ptr<ExprSeqNode>
		bool opt = true;
		while(parseExpr(opt) is ExprNode e)
        {
			exprs.push_back(e);
			if(toker.curr != (Keyword)',') break;
			toker.next();
			opt = false;
		}
		return exprs/*.release()*/;
	}

	private ExprNode parseExpr(bool opt)
	{
		if(toker.curr == Keyword.NOT)
		{
			toker.next();
			ExprNode expr = parseExpr1(false);
			return new RelExprNode((Keyword)'=', expr, new IntConstNode(0));
		}
		return parseExpr1(opt);
	}
	private ExprNode parseExpr1(bool opt) //And, Or, Eor
	{
		ExprNode lhs = parseExpr2(opt);//a_ptr<ExprNode>
		if(lhs is null) return null;
		for(; ; )
		{
			Keyword c = toker.curr;
			if(c != Keyword.AND && c != Keyword.OR && c != Keyword.XOR) return lhs/*.release()*/;
			toker.next();
			ExprNode rhs = parseExpr2(false);
			lhs = new BinExprNode(c, lhs/*.release()*/, rhs);
		}
	}
	private ExprNode parseExpr2(bool opt) //<,=,>,<=,<>,>=
	{
		ExprNode lhs = parseExpr3(opt);//a_ptr<ExprNode>
		if(lhs is null) return null;
		for(; ; )
		{
			Keyword c = toker.curr;
			if(c != (Keyword)'<' && c != (Keyword)'>' && c != (Keyword)'=' && c != Keyword.LE && c != Keyword.GE && c != Keyword.NE) return lhs/*.release()*/;
			toker.next();
			ExprNode rhs = parseExpr3(false);
			lhs = new RelExprNode(c, lhs/*.release()*/, rhs);
		}
	}
	private ExprNode parseExpr3(bool opt) //+,-
	{
		ExprNode lhs = parseExpr4(opt);//a_ptr<ExprNode>
		if(lhs is null) return null;
		for(; ; )
		{
			Keyword c = toker.curr;
			if(c != (Keyword)'+' && c != (Keyword)'-') return lhs/*.release()*/;
			toker.next();
			ExprNode rhs = parseExpr4(false);
			lhs = new ArithExprNode(c, lhs/*.release()*/, rhs);
		}
	}
	private ExprNode parseExpr4(bool opt) //Lsr,Lsr,Asr
	{
		ExprNode lhs = parseExpr5(opt);//a_ptr<ExprNode>
		if(lhs is null) return null;
		for(; ; )
		{
			Keyword c = toker.curr;
			if(c != Keyword.SHL && c != Keyword.SHR && c != Keyword.SAR) return lhs/*.release()*/;
			toker.next();
			ExprNode rhs = parseExpr5(false);
			lhs = new BinExprNode(c, lhs/*.release()*/, rhs);
		}
	}
	private ExprNode parseExpr5(bool opt) //*,/,Mod
	{
		ExprNode lhs = parseExpr6(opt);//a_ptr<ExprNode>
		if(lhs is null) return null;
		for(; ; )
		{
			Keyword c = toker.curr;
			if(c != (Keyword)'*' && c != (Keyword)'/' && c != Keyword.MOD) return lhs/*.release()*/;
			toker.next();
			ExprNode rhs = parseExpr6(false);
			lhs = new ArithExprNode(c, lhs/*.release()*/, rhs);
		}
	}
	private ExprNode parseExpr6(bool opt) //^
	{
		ExprNode lhs = parseUniExpr(opt);//a_ptr<ExprNode>
		if(lhs is null) return null;
		for(; ; )
		{
			Keyword c = toker.curr;
			if(c != (Keyword)'^') return lhs/*.release()*/;
			toker.next();
			ExprNode rhs = parseUniExpr(false);
			lhs = new ArithExprNode(c, lhs/*.release()*/, rhs);
		}
	}
	private ExprNode parseUniExpr(bool opt) //+,-,Not,~
	{
		ExprNode result = null;
		string t;

		Keyword c = (Keyword)toker.curr;
		switch(c)
		{
			case Keyword.BBINT:
				if(toker.next() == (Keyword)'%') toker.next();
				result = parseUniExpr(false);
				result = new CastNode(result, Type.int_type);
				break;
			case Keyword.BBFLOAT:
				if(toker.next() == (Keyword)'#') toker.next();
				result = parseUniExpr(false);
				result = new CastNode(result, Type.float_type);
				break;
			case Keyword.BBSTR:
				if(toker.next() == (Keyword)'$') toker.next();
				result = parseUniExpr(false);
				result = new CastNode(result, Type.string_type);
				break;
			case Keyword.OBJECT:
				if(toker.next() == (Keyword)'.') toker.next();
				t = parseIdent();
				result = parseUniExpr(false);
				result = new ObjectCastNode(result, t);
				break;
			case Keyword.BBHANDLE:
				toker.next();
				result = parseUniExpr(false);
				result = new ObjectHandleNode(result);
				break;
			case Keyword.BEFORE:
				toker.next();
				result = parseUniExpr(false);
				result = new BeforeNode(result);
				break;
			case Keyword.AFTER:
				toker.next();
				result = parseUniExpr(false);
				result = new AfterNode(result);
				break;
			case (Keyword)'+':
			case (Keyword)'-':
			case (Keyword)'~':
			case Keyword.ABS:
			case Keyword.SGN:
				toker.next();
				result = parseUniExpr(false);
				if(c == (Keyword)'~')
				{
					result = new BinExprNode(Keyword.XOR, result, new IntConstNode(-1));
				}
				else
				{
					result = new UniExprNode(c, result);
				}
				break;
			default:
				result = parsePrimary(opt);
				break;
		}
		return result;
	}
	private ExprNode parsePrimary(bool opt)
	{
		ExprNode expr;//a_ptr<ExprNode>
		string t, ident, tag;
		ExprNode result = null;
		int n, k;

		switch(toker.curr)
		{
			case (Keyword)'(':
				toker.next();
				expr = parseExpr(false);
				if(toker.curr != (Keyword)')') throw exp("')'");
				toker.next();
				result = expr/*.release()*/;
				break;
			case Keyword.BBNEW:
				toker.next();
				t = parseIdent();
				result = new NewNode(t);
				break;
			case Keyword.FIRST:
				toker.next();
				t = parseIdent();
				result = new FirstNode(t);
				break;
			case Keyword.LAST:
				toker.next();
				t = parseIdent();
				result = new LastNode(t);
				break;
			case Keyword.BBNULL:
				result = new NullNode();
				toker.next();
				break;
			case Keyword.INTCONST:
				result = new IntConstNode(int.Parse(toker.text));//atoi
				toker.next();
				break;
			case Keyword.FLOATCONST:
				result = new FloatConstNode(float.Parse(toker.text));//atof
				toker.next();
				break;
			case Keyword.STRINGCONST:
				t = toker.text;
				result = new StringConstNode(t.Substring(1, t.Length - 2));
				toker.next();
				break;
			case Keyword.BINCONST:
				n = 0;
				t = toker.text;
				for(k = 1; k < t.Length; ++k) n = (n << 1) | ((t[k] == '1')?1:0);
				result = new IntConstNode(n);
				toker.next();
				break;
			case Keyword.HEXCONST:
				n = 0;
				t = toker.text;
				for(k = 1; k < t.Length; ++k) n = (n << 4) | (char.IsDigit(t[k]) ? t[k] & 0xf : (t[k] & 7) + 9);
				result = new IntConstNode(n);
				toker.next();
				break;
			case Keyword.PI:
				result = new FloatConstNode(3.1415926535897932384626433832795f);
				toker.next();
				break;
			case Keyword.BBTRUE:
				result = new IntConstNode(1);
				toker.next();
				break;
			case Keyword.BBFALSE:
				result = new IntConstNode(0);
				toker.next();
				break;
			case Keyword.IDENT:
				ident = toker.text;
				toker.next();
				tag = parseTypeTag();
				if(toker.curr == (Keyword)'(' && !arrayDecls.ContainsKey(ident))
				{
					//must be a func
					toker.next();
					ExprSeqNode exprs = parseExprSeq();//a_ptr<ExprSeqNode>
					if(toker.curr != (Keyword)')') throw exp("')'");
					toker.next();
					result = new CallNode(ident, tag, exprs/*.release()*/);
				}
				else
				{
					//must be a var
					VarNode var = parseVar(ident, tag);
					result = new VarExprNode(var);
				}
				break;
			default:
				if(!opt)
					throw exp("expression");
				break;
		}
		return result;
	}
}