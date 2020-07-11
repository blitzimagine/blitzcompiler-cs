using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Blitz3D.Parsing.Nodes;

namespace Blitz3D.Parsing
{
	///<summary>The parser builds an abstact syntax tree from input tokens.</summary>
	public class Parser
	{
		private enum STMTS
		{
			PROG,
			BLOCK,
			LINE
		}

		private string incfile;
		public readonly Dictionary<string,IncludeFileNode> included = new Dictionary<string,IncludeFileNode>();

		private Tokenizer toker;
		private Dictionary<string, DimNode> arrayDecls = new Dictionary<string, DimNode>();

		private DeclSeqNode consts;
		private DeclSeqNode structs;
		private DeclSeqNode funcs;
		private DeclSeqNode datas;

		private static bool isTerm(Keyword c) => c == (Keyword)':' || c == Keyword.NEWLINE;

		public Parser(Tokenizer t) => toker = t;

		public ProgNode parse(string main)
		{
			incfile = main;

			consts = new DeclSeqNode();
			structs = new DeclSeqNode();
			funcs = new DeclSeqNode();
			datas = new DeclSeqNode();

			StmtSeqNode stmts = parseStmtSeq(STMTS.PROG);	
			
			toker.AssertCurr(Keyword.EOF, exp, "end-of-file");

			return new ProgNode(consts, structs, funcs, datas, stmts);
		}

		private StmtSeqNode parseStmtSeq(STMTS scope)
		{
			StmtSeqNode stmts = new StmtSeqNode(incfile);
			string lastLabel = null;
			for(;;)
			{
				while(toker.Curr == (Keyword)':' || (scope != STMTS.LINE && toker.Curr == Keyword.NEWLINE))
				{
					toker.Next();
				}
				StmtNode result = null;

				Point pos = toker.Pos;

				Keyword currKeyWord = toker.Curr;
				switch(currKeyWord)
				{
					case Keyword.INCLUDE:
					{
						toker.AssertNext(Keyword.STRINGCONST, exp, "include filename");

						string inc = toker.TakeText();
						inc = inc.Substring(1, inc.Length - 2);

						//WIN32 KLUDGE//
						inc = Path.GetFullPath(inc);
						inc = inc.ToLowerInvariant();

						if(!included.TryGetValue(inc, out var include))
						{
							using StreamReader i_stream = new StreamReader(inc);

							Tokenizer i_toker = new Tokenizer(i_stream);

							string t_inc = incfile;
							incfile = inc;
							Tokenizer t_toker = toker;
							toker = i_toker;

							include = new IncludeFileNode(inc);
							included.Add(incfile, include);

							include.stmts = parseStmtSeq(scope);
							toker.AssertCurr(Keyword.EOF, exp, "end-of-file");

							toker = t_toker;
							incfile = t_inc;
						}
						result = new IncludeNode(incfile, include);
					}
					break;
					case Keyword.IDENT:
					{
						string ident = toker.TakeText();
						string tag = parseTypeTag();
						if(!arrayDecls.ContainsKey(ident) && toker.Curr != Keyword.EQ && toker.Curr != Keyword.Backslash && toker.Curr != Keyword.BracketOpen)
						{
							//must be a function
							ExprSeqNode exprs;
							if(toker.Curr == Keyword.ParenOpen)
							{
								//ugly lookahead for optional '()' around statement params
								int nest = 1, k;
								for(k = 1; ; ++k)
								{
									Keyword c = toker.LookAhead(k);
									if(isTerm(c)) throw ex("Mismatched brackets");
									else if(c == Keyword.ParenOpen) ++nest;
									else if(c == Keyword.ParenClose && (--nest)==0) break;
								}
								if(isTerm(toker.LookAhead(++k)))
								{
									toker.Next();
									exprs = parseExprSeq();
									toker.AssertSkip(Keyword.ParenClose, exp,"')'");
								}
								else
								{
									exprs = parseExprSeq();
								}
							}
							else exprs = parseExprSeq();
							CallNode call = new CallNode(ident, tag, exprs);
							result = new ExprStmtNode(call);
						}
						else
						{
							//must be a var
							VarNode var = parseVar(ident, tag);
							toker.AssertSkip(Keyword.EQ, exp, "variable assignment");
							ExprNode expr = parseExpr(false);
							result = new AsgnNode(var, expr);
						}
					}
					break;
					case Keyword.IF:
					{
						toker.Next();
						result = parseIf();
						if(toker.Curr == Keyword.ENDIF) toker.Next();
					}
					break;
					case Keyword.WHILE:
					{
						toker.Next();
						ExprNode expr = parseExpr(false);
						StmtSeqNode stmts2 = parseStmtSeq(STMTS.BLOCK);
						toker.AssertSkip(Keyword.WEND, exp, "'Wend'");
						result = new WhileNode(expr, stmts2);
					}
					break;
					case Keyword.REPEAT:
					{
						toker.Next();
						ExprNode expr = null;
						StmtSeqNode stmts2 = parseStmtSeq(STMTS.BLOCK);
						Keyword curr = toker.Curr;
						if(curr != Keyword.UNTIL && curr != Keyword.FOREVER) throw exp("'Until' or 'Forever'");
						toker.Next();
						if(curr == Keyword.UNTIL) expr = parseExpr(false);
						result = new RepeatNode(stmts2, expr);
					}
					break;
					case Keyword.SELECT:
					{
						toker.Next();
						ExprNode expr = parseExpr(false);
						SelectNode selNode = new SelectNode(expr);
						for(;;)
						{
							toker.SkipWhile(isTerm);
							if(toker.TrySkip(Keyword.CASE))
							{
								ExprSeqNode exprs = parseExprSeq();
								if(exprs.Count==0) throw exp("expression sequence");
								StmtSeqNode stmts2 = parseStmtSeq(STMTS.BLOCK);
								selNode.Add(new CaseNode(exprs, stmts2));
								continue;
							}
							else if(toker.TrySkip(Keyword.DEFAULT))
							{
								selNode.defStmts = parseStmtSeq(STMTS.BLOCK);
								continue;
							}
							else if(toker.TrySkip(Keyword.ENDSELECT))
							{
								break;
							}
							throw exp("'Case', 'Default' or 'End Select'");
						}
						result = selNode;
					}
					break;
					case Keyword.FOR:
					{
						VarNode var;
						StmtSeqNode stmts2;
						toker.Next();
						var = parseVar();
						toker.AssertSkip(Keyword.EQ, exp, "variable assignment");
						if(toker.TrySkip(Keyword.EACH))
						{
							string ident = parseIdent();
							stmts2 = parseStmtSeq(STMTS.BLOCK);
							toker.AssertSkip(Keyword.NEXT, exp, "'Next'");
							result = new ForEachNode(var, ident, stmts2);
						}
						else
						{
							ExprNode from, to, step;
							from = parseExpr(false);
							toker.AssertSkip(Keyword.TO, exp, "'TO'");
							to = parseExpr(false);
							//step...
							if(toker.Curr == Keyword.STEP)
							{
								toker.Next();
								step = parseExpr(false);
							}
							else step = new IntConstNode(1);
							stmts2 = parseStmtSeq(STMTS.BLOCK);
							toker.AssertSkip(Keyword.NEXT, exp, "'Next'");
							result = new ForNode(var, from, to, step, stmts2);
						}
					}
					break;
					case Keyword.EXIT:
					{
						toker.Next();
						result = new ExitNode();
					}
					break;
					case Keyword.GOTO:
					{
						toker.Next();
						result = new GotoNode(parseIdent());
					}
					break;
					case Keyword.GOSUB:
					{
						toker.Next();
						result = new GosubNode(parseIdent());
					}
					break;
					case Keyword.RETURN:
					{
						toker.Next();
						result = new ReturnNode(parseExpr(true));
					}
					break;
					case Keyword.BBDELETE:
					{
						if(toker.Next() == Keyword.EACH)
						{
							toker.Next();
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
						toker.Next();
						ExprNode expr1 = parseExpr(false);
						if(toker.Curr != Keyword.BEFORE && toker.Curr != Keyword.AFTER) throw exp("'Before' or 'After'");
						bool before = toker.Curr == Keyword.BEFORE;
						toker.Next();
						ExprNode expr2 = parseExpr(false);
						result = new InsertNode(expr1, expr2, before);
					}
					break;
					case Keyword.READ:
						do
						{
							toker.Next();
							VarNode var = parseVar();
							StmtNode stmt = new ReadNode(var);
							stmt.pos = pos;
							pos = toker.Pos;
							stmts.Add(stmt);
						} while(toker.Curr == Keyword.COMMA);
						break;
					case Keyword.RESTORE:
						if(toker.Next() == Keyword.IDENT)
						{
							result = new RestoreNode(toker.TakeText());
						}
						else result = new RestoreNode("");
						break;
					case Keyword.DATA:
						if(scope != STMTS.PROG) throw ex("'Data' can only appear in main program");
						do
						{
							toker.Next();
							ExprNode expr = parseExpr(false);
							datas.Add(new DataDeclNode(expr, lastLabel));
						} while(toker.Curr == Keyword.COMMA);
						break;
					case Keyword.TYPE:
						if(scope != STMTS.PROG) throw ex("'Type' can only appear in main program");
						toker.Next();
						structs.Add(parseStructDecl());
						break;
					case Keyword.BBCONST:
						if(scope != STMTS.PROG) throw ex("'Const' can only appear in main program");
						do
						{
							toker.Next();
							consts.Add(parseVarDecl(DECL.GLOBAL, true));
						} while(toker.Curr == Keyword.COMMA);
						break;
					case Keyword.FUNCTION:
						if(scope != STMTS.PROG) throw ex("'Function' can only appear in main program");
						toker.Next();
						funcs.Add(parseFuncDecl());
						break;
					case Keyword.DIM:
						do
						{
							toker.Next();
							StmtNode stmt = parseArrayDecl();
							stmt.pos = pos;
							pos = toker.Pos;
							stmts.Add(stmt);
						} while(toker.Curr == Keyword.COMMA);
						break;
					case Keyword.LOCAL:
						do
						{
							toker.Next();
							DeclNode d = parseVarDecl(DECL.LOCAL, false);
							StmtNode stmt = new DeclStmtNode(d);
							stmt.pos = pos;
							pos = toker.Pos;
							stmts.Add(stmt);
						} while(toker.Curr == Keyword.COMMA);
						break;
					case Keyword.GLOBAL:
						if(scope != STMTS.PROG) throw ex("'Global' can only appear in main program");
						do
						{
							toker.Next();
							DeclNode d = parseVarDecl(DECL.GLOBAL, false);
							StmtNode stmt = new DeclStmtNode(d);
							stmt.pos = pos;
							pos = toker.Pos;
							stmts.Add(stmt);
						} while(toker.Curr == Keyword.COMMA);
						break;
					case (Keyword)'.':
					{
						toker.Next();
						string t = parseIdent();
						result = new LabelNode(t, datas.Count);
						lastLabel = t;
					}
					break;
					default:
						return stmts;
				}

				if(result!=null)
				{
					result.pos = pos;
					stmts.Add(result);
				}
				if(lastLabel!=null)
				{
					if(currKeyWord!=Keyword.DATA && currKeyWord!=(Keyword)'.')
					{
						lastLabel = null;
					}
				}
			}
		}

		private Ex ex(string s) => new Ex(s, toker.Pos, incfile);
		private Ex exp(string s) => toker.Curr switch
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
			if(toker.Curr != Keyword.IDENT)
			{
				throw exp("identifier");
			}
			return toker.TakeText();
		}

		private string parseTypeTag()
		{
			switch(toker.Curr)
			{
				case (Keyword)'%': toker.Next(); return "%";
				case (Keyword)'#': toker.Next(); return "#";
				case (Keyword)'$': toker.Next(); return "$";
				case (Keyword)'.': toker.Next(); return parseIdent();
				default: return "";
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
			VarNode var;
			if(toker.Curr == Keyword.ParenOpen)
			{
				toker.Next();
				ExprSeqNode exprs = parseExprSeq();
				toker.AssertSkip(Keyword.ParenClose, exp, "')'");
				var = new ArrayVarNode(ident, tag, exprs);
			}
			else var = new IdentVarNode(ident, tag);

			for(;;)
			{
				if(toker.Curr == Keyword.Backslash)
				{
					toker.Next();
					string ident2 = parseIdent();
					string tag2 = parseTypeTag();
					ExprNode expr = new VarExprNode(var);
					var = new FieldVarNode(expr, ident2, tag2);
				}
				else if(toker.Curr == Keyword.BracketOpen)
				{
					toker.Next();
					ExprSeqNode exprs = parseExprSeq();
					if(exprs.exprs.Count != 1 || toker.Curr != Keyword.BracketClose) throw exp("']'");
					toker.Next();
					ExprNode expr = new VarExprNode(var);
					var = new VectorVarNode(expr, exprs);
				}
				else
				{
					break;
				}
			}
			return var;
		}

		private IfNode parseIf()
		{
			ExprNode expr;
			StmtSeqNode stmts, elseOpt = null;

			expr = parseExpr(false);
			if(toker.Curr == Keyword.THEN) toker.Next();

			bool blkif = isTerm(toker.Curr);
			stmts = parseStmtSeq(blkif ? STMTS.BLOCK : STMTS.LINE);

			if(toker.Curr == Keyword.ELSEIF)
			{
				Point pos = toker.Pos;
				toker.Next();
				IfNode ifnode = parseIf();
				ifnode.pos = pos;
				elseOpt = new StmtSeqNode(incfile);
				elseOpt.Add(ifnode);
			}
			else if(toker.Curr == Keyword.ELSE)
			{
				toker.Next();
				elseOpt = parseStmtSeq(blkif ? STMTS.BLOCK : STMTS.LINE);
			}
			if(blkif)
			{
				toker.AssertCurr(Keyword.ENDIF, exp, "'EndIf'");
			}
			else
			{
				toker.AssertCurr(Keyword.NEWLINE, exp, "end-of-line");
			}

			return new IfNode(expr, stmts, elseOpt);
		}

		private DeclNode parseVarDecl(DECL kind, bool constant)
		{
			Point pos = toker.Pos;
			string ident = parseIdent();
			string tag = parseTypeTag();
			DeclNode d;
			if(toker.TrySkip(Keyword.BracketOpen))
			{
				if(constant)
				{
					throw ex("Blitz arrays may not be constant");
				}
				ExprSeqNode exprs = parseExprSeq();
				if(exprs.Count != 1 || toker.Curr != Keyword.BracketClose)
				{
					throw exp("']'");
				}
				toker.Next();
				d = new VectorDeclNode(ident, tag, exprs, kind);
			}
			else
			{
				ExprNode expr = null;
				if(toker.Curr == Keyword.EQ)
				{
					toker.Next();
					expr = parseExpr(false);
				}
				else if(constant)
				{
					throw ex("Constants must be initialized");
				}
				d = new VarDeclNode(ident, tag, kind, constant, expr);
			}
			d.pos = pos;
			d.file = incfile;
			return d;
		}
		private DimNode parseArrayDecl()
		{
			Point pos = toker.Pos;
			string ident = parseIdent();
			string tag = parseTypeTag();
			toker.AssertSkip(Keyword.ParenOpen, exp, "'('");
			ExprSeqNode exprs = parseExprSeq();
			toker.AssertSkip(Keyword.ParenClose, exp, "')'");
			if(exprs.Count==0)
			{
				throw ex("can't have a 0 dimensional array");
			}
			DimNode d = new DimNode(ident, tag, exprs);
			arrayDecls[ident] = d;
			d.pos = pos;
			return d;
		}
		private DeclNode parseFuncDecl()
		{
			Point pos = toker.Pos;
			string ident = parseIdent();
			string tag = parseTypeTag();

			DeclSeqNode @params = new DeclSeqNode();
			toker.AssertSkip(Keyword.ParenOpen, exp, "'('");
			if(!toker.TrySkip(Keyword.ParenClose))
			{
				for(;;)
				{
					@params.Add(parseVarDecl(DECL.PARAM, false));
					if(toker.Curr != Keyword.COMMA) break;
					toker.Next();
				}
				toker.AssertSkip(Keyword.ParenClose, exp, "')'");
			}

			StmtSeqNode stmts = parseStmtSeq(STMTS.BLOCK);
			if(toker.Curr != Keyword.ENDFUNCTION)
			{
				throw exp("'End Function'");
			}
			StmtNode ret = new ReturnNode(null);
			ret.pos = toker.Pos;
			stmts.Add(ret);
			toker.Next();
			DeclNode d = new FuncDeclNode(ident, tag, @params, stmts);
			d.pos = pos;
			d.file = incfile;
			return d;
		}
		private DeclNode parseStructDecl()
		{
			Point pos = toker.Pos;
			string ident = parseIdent();
			toker.SkipWhile(Keyword.NEWLINE);
			DeclSeqNode fields = new DeclSeqNode();
			while(toker.TrySkip(Keyword.FIELD))
			{
				do
				{
					fields.Add(parseVarDecl(DECL.FIELD, false));
				} while(toker.TrySkip(Keyword.COMMA));
				toker.SkipWhile(Keyword.NEWLINE);
			}
			toker.AssertSkip(Keyword.ENDTYPE, exp, "'Field' or 'End Type'");
			DeclNode d = new StructDeclNode(ident, fields);
			d.pos = pos;
			d.file = incfile;
			return d;
		}

		private ExprSeqNode parseExprSeq()
		{
			ExprSeqNode exprs = new ExprSeqNode();
			bool opt = true;
			while(parseExpr(opt) is ExprNode e)
			{
				exprs.Add(e);
				if(!toker.TrySkip(Keyword.COMMA))
				{
					break;
				}
				opt = false;
			}
			return exprs;
		}

		private ExprNode parseExpr(bool opt)
		{
			if(toker.TrySkip(Keyword.NOT))
			{
				ExprNode expr = parseExpr1(false);
				return new RelExprNode(Keyword.EQ, expr, new IntConstNode(0));
			}
			return parseExpr1(opt);
		}
		private ExprNode parseExpr1(bool opt) //And, Or, Eor
		{
			ExprNode lhs = parseExpr2(opt);
			if(lhs is null) return null;

			while(toker.TryTake(out Keyword c, Keyword.AND, Keyword.OR, Keyword.XOR))
			{
				ExprNode rhs = parseExpr2(false);
				lhs = new BinExprNode(c, lhs, rhs);
			}
			return lhs;
		}
		private ExprNode parseExpr2(bool opt) //<,=,>,<=,<>,>=
		{
			ExprNode lhs = parseExpr3(opt);
			if(lhs is null) return null;

			while(toker.TryTake(out Keyword c, Keyword.LT, Keyword.GT, Keyword.EQ, Keyword.LE, Keyword.GE, Keyword.NE))
			{
				ExprNode rhs = parseExpr3(false);
				lhs = new RelExprNode(c, lhs, rhs);
			}
			return lhs;
		}
		private ExprNode parseExpr3(bool opt) //+,-
		{
			ExprNode lhs = parseExpr4(opt);
			if(lhs is null) return null;

			while(toker.TryTake(out Keyword c, Keyword.ADD, Keyword.SUB))
			{
				ExprNode rhs = parseExpr4(false);
				lhs = new ArithExprNode(c, lhs, rhs);
			}
			return lhs;
		}
		private ExprNode parseExpr4(bool opt) //Lsr,Lsr,Asr
		{
			ExprNode lhs = parseExpr5(opt);
			if(lhs is null) return null;

			while(toker.TryTake(out Keyword c, Keyword.SHL, Keyword.SHR, Keyword.SAR))
			{
				ExprNode rhs = parseExpr5(false);
				lhs = new BinExprNode(c, lhs, rhs);
			}
			return lhs;
		}
		private ExprNode parseExpr5(bool opt) //*,/,Mod
		{
			ExprNode lhs = parseExpr6(opt);
			if(lhs is null) return null;
			while(toker.TryTake(out Keyword c, Keyword.MUL, Keyword.DIV, Keyword.MOD))
			{
				ExprNode rhs = parseExpr6(false);
				lhs = new ArithExprNode(c, lhs, rhs);
			}
			return lhs;
		}
		private ExprNode parseExpr6(bool opt) //^
		{
			ExprNode lhs = parseUniExpr(opt);
			if(lhs is null) return null;
			while(toker.TryTake(out Keyword c, Keyword.POW))
			{
				ExprNode rhs = parseUniExpr(false);
				lhs = new ArithExprNode(c, lhs, rhs);
			}
			return lhs;
		}

		private ExprNode parseUniExpr(bool opt) //+,-,Not,~
		{
			ExprNode result;

			Keyword c = toker.Curr;
			switch(c)
			{
				case Keyword.BBINT:
					toker.Next();
					toker.TrySkip((Keyword)'%');
					result = parseUniExpr(false);
					result = new CastNode(result, Type.Int);
					break;
				case Keyword.BBFLOAT:
					toker.Next();
					toker.TrySkip((Keyword)'#');
					result = parseUniExpr(false);
					result = new CastNode(result, Type.Float);
					break;
				case Keyword.BBSTR:
					toker.Next();
					toker.TrySkip((Keyword)'$');
					result = parseUniExpr(false);
					result = new CastNode(result, Type.String);
					break;
				case Keyword.OBJECT:
					toker.Next();
					toker.TrySkip((Keyword)'.');
					string t = parseIdent();
					result = parseUniExpr(false);
					result = new ObjectCastNode(result, t);
					break;
				case Keyword.BBHANDLE:
					toker.Next();
					result = parseUniExpr(false);
					result = new ObjectHandleNode(result);
					break;
				case Keyword.BEFORE:
					toker.Next();
					result = parseUniExpr(false);
					result = new BeforeNode(result);
					break;
				case Keyword.AFTER:
					toker.Next();
					result = parseUniExpr(false);
					result = new AfterNode(result);
					break;
				case Keyword.POSITIVE:
				case Keyword.NEGATIVE:
				case Keyword.BITNOT:
				case Keyword.ABS:
				case Keyword.SGN:
					toker.Next();
					result = parseUniExpr(false);
					if(c == Keyword.BITNOT)
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
			switch(toker.Curr)
			{
				case Keyword.ParenOpen:
				{
					toker.Next();
					ExprNode expr = parseExpr(false);
					toker.AssertSkip(Keyword.ParenClose, exp, "')'");
					return expr;
				}
				case Keyword.BBNEW:
				{
					toker.Next();
					return new NewNode(parseIdent());
				}
				case Keyword.FIRST:
				{
					toker.Next();
					return new FirstNode(parseIdent());
				}
				case Keyword.LAST:
				{
					toker.Next();
					return new LastNode(parseIdent());
				}
				case Keyword.BBNULL:
					toker.Next();
					return new NullNode();
				case Keyword.INTCONST:
				{
					return new IntConstNode(toker.Take(int.Parse));
				}
				case Keyword.FLOATCONST:
				{
					return new FloatConstNode(toker.Take(float.Parse));
				}
				case Keyword.STRINGCONST:
				{
					//Trim the quotes
					string t = toker.TakeText();
					return new StringConstNode(t.Substring(1, t.Length - 2));
				}
				case Keyword.BINCONST:
				{
					return new IntConstNode(Convert.ToInt32(toker.TakeText(),2));
				}
				case Keyword.HEXCONST:
				{
					return new IntConstNode(Convert.ToInt32(toker.TakeText(),16));
				}
				case Keyword.PI:
					toker.Next();
					return new FloatConstNode(MathF.PI /*3.1415926535897932384626433832795f*/);
				case Keyword.BBTRUE:
					toker.Next();
					return new IntConstNode(1);
				case Keyword.BBFALSE:
					toker.Next();
					return new IntConstNode(0);
				case Keyword.IDENT:
					string ident = toker.TakeText();
					string tag = parseTypeTag();
					if(toker.Curr == Keyword.ParenOpen && !arrayDecls.ContainsKey(ident))
					{
						//must be a func
						toker.Next();
						ExprSeqNode exprs = parseExprSeq();
						toker.AssertSkip(Keyword.ParenClose, exp, "')'");
						return new CallNode(ident, tag, exprs);
					}
					else
					{
						//must be a var
						VarNode var = parseVar(ident, tag);
						return new VarExprNode(var);
					}
				default:
					if(!opt)
					{
						throw exp("expression");
					}
					return null;
			}
		}
	}
}