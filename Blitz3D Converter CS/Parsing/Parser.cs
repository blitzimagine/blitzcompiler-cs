using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Blitz3D.Converter.Parsing.Nodes;

namespace Blitz3D.Converter.Parsing
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
		public readonly Dictionary<string,FileNode> included = new Dictionary<string,FileNode>();

		private Tokenizer toker;
		private Dictionary<string, DimNode> arrayDecls = new Dictionary<string, DimNode>(StringComparer.OrdinalIgnoreCase);

		private DeclSeqNode consts;
		private DeclSeqNode structs;
		private DeclSeqNode funcs;
		private DeclSeqNode datas;

		private static bool isTerm(TokenType c) => c == (TokenType)':' || c == TokenType.NEWLINE;

		public Parser(Tokenizer t) => toker = t;

		public ProgNode parse(string main)
		{
			incfile = main;

			consts = new DeclSeqNode();
			structs = new DeclSeqNode();
			funcs = new DeclSeqNode();
			datas = new DeclSeqNode();

			StmtSeqNode stmts = parseStmtSeq(STMTS.PROG);	
			
			if(toker.CurrType!=TokenType.EOF)
			{
				throw exp("end-of-file");
			}

			return new ProgNode(consts, structs, funcs, datas, stmts);
		}

		private StmtSeqNode parseStmtSeq(STMTS scope)
		{
			StmtSeqNode stmts = new StmtSeqNode(incfile);
			string lastLabel = null;
			for(;;)
			{
				while(toker.CurrType == (TokenType)':' || (scope != STMTS.LINE && toker.CurrType == TokenType.NEWLINE))
				{
					string text = toker.TakeText();
					if(toker.CurrType == TokenType.NEWLINE)
					{
						stmts.AddComment(text);
					}
				}
				StmtNode result = null;

				TokenType currKeyWord = toker.CurrType;
				switch(currKeyWord)
				{
					case TokenType.INCLUDE:
					{
						toker.NextType();

						string inc = toker.TakeText();
						inc = inc.Substring(1, inc.Length - 2).ToLowerInvariant();

						string includeClassPath = inc;

						if(!included.TryGetValue(inc, out var include))
						{
							using StreamReader i_stream = new StreamReader(inc);

							Tokenizer i_toker = new Tokenizer(i_stream);

							string t_inc = incfile;
							incfile = inc;
							Tokenizer t_toker = toker;
							toker = i_toker;

							include = new FileNode(includeClassPath);
							included.Add(incfile, include);
							
							//Assign stmts after adding to dictionary so we know that it already exists.
							include.stmts = parseStmtSeq(scope);

							if(toker.CurrType!=TokenType.EOF)
							{
								throw exp("end-of-file");
							}

							toker = t_toker;
							incfile = t_inc;
						}
						result = new IncludeNode(include);
					}
					break;
					case TokenType.IDENT:
					{
						string ident = toker.TakeText();
						string tag = parseTypeTag();
						if(!arrayDecls.ContainsKey(ident) && toker.CurrType != TokenType.EQ && toker.CurrType != TokenType.Backslash && toker.CurrType != TokenType.BracketOpen)
						{
							//must be a function
							ExprSeqNode exprs;
							if(toker.CurrType == TokenType.ParenOpen)
							{
								//ugly lookahead for optional '()' around statement params
								int nest = 1;
								int k;
								for(k = 1; nest>0; k++)
								{
									TokenType c = toker.LookAhead(k);
									if(isTerm(c))
									{
										throw ex("Mismatched brackets");
									}
									else if(c == TokenType.ParenOpen)
									{
										nest++;
									}
									else if(c == TokenType.ParenClose)
									{
										nest--;
									}
								}
								if(isTerm(toker.LookAhead(k)))
								{
									toker.NextType();
									exprs = parseExprSeq();
									toker.AssertSkip(TokenType.ParenClose, exp,"')'");
								}
								else
								{
									exprs = parseExprSeq();
								}
							}
							else
							{
								exprs = parseExprSeq();
							}
							CallNode call = new CallNode(ident, tag, exprs);
							result = new ExprStmtNode(call);
						}
						else
						{
							//must be a var
							VarNode var = parseVar(ident, tag);
							toker.AssertSkip(TokenType.EQ, exp, "variable assignment");
							ExprNode expr = parseExpr(false);
							result = new AsgnNode(var, expr);
						}
					}
					break;
					case TokenType.IF:
					{
						toker.NextType();
						result = parseIf();
						toker.TrySkip(TokenType.ENDIF);
					}
					break;
					case TokenType.WHILE:
					{
						toker.NextType();
						ExprNode expr = parseExpr(false);
						StmtSeqNode stmts2 = parseStmtSeq(STMTS.BLOCK);
						toker.AssertSkip(TokenType.WEND, exp, "'Wend'");
						result = new WhileNode(expr, stmts2);
					}
					break;
					case TokenType.REPEAT:
					{
						toker.NextType();
						ExprNode expr = null;
						StmtSeqNode stmts2 = parseStmtSeq(STMTS.BLOCK);
						TokenType curr = toker.CurrType;
						if(curr != TokenType.UNTIL && curr != TokenType.FOREVER)
						{
							throw exp("'Until' or 'Forever'");
						}
						toker.NextType();
						if(curr == TokenType.UNTIL) expr = parseExpr(false);
						result = new RepeatNode(stmts2, expr);
					}
					break;
					case TokenType.SELECT:
					{
						toker.NextType();
						ExprNode expr = parseExpr(false);
						SelectNode selNode = new SelectNode(expr);
						while(!toker.TrySkip(TokenType.END_SELECT))
						{
							toker.SkipWhile(isTerm);
							if(toker.TrySkip(TokenType.CASE))
							{
								ExprSeqNode exprs = parseExprSeq();
								StmtSeqNode stmts2 = parseStmtSeq(STMTS.BLOCK);
								selNode.Add(new CaseNode(exprs, stmts2));
							}
							else if(toker.TrySkip(TokenType.DEFAULT))
							{
								selNode.Add(new DefaultCaseNode(parseStmtSeq(STMTS.BLOCK)));
							}
							else
							{
								throw exp("'Case', 'Default' or 'End Select'");
							}
						}
						result = selNode;
					}
					break;
					case TokenType.FOR:
					{
						VarNode var;
						StmtSeqNode stmts2;
						toker.NextType();
						var = parseVar();
						toker.AssertSkip(TokenType.EQ, exp, "variable assignment");
						if(toker.TrySkip(TokenType.EACH))
						{
							string ident = parseIdent();
							stmts2 = parseStmtSeq(STMTS.BLOCK);
							toker.AssertSkip(TokenType.NEXT, exp, "'Next'");
							result = new ForEachNode(var, ident, stmts2);
						}
						else
						{
							ExprNode from, to, step;
							from = parseExpr(false);
							toker.AssertSkip(TokenType.TO, exp, "'TO'");
							to = parseExpr(false);
							//step...
							if(toker.CurrType == TokenType.STEP)
							{
								toker.NextType();
								step = parseExpr(false);
							}
							else step = new IntConstNode("1");
							stmts2 = parseStmtSeq(STMTS.BLOCK);
							toker.AssertSkip(TokenType.NEXT, exp, "'Next'");
							result = new ForNode(var, from, to, step, stmts2);
						}
					}
					break;
					case TokenType.EXIT:
					{
						toker.NextType();
						result = new ExitNode();
					}
					break;
					case TokenType.GOTO:
					{
						toker.NextType();
						result = new GotoNode(parseIdent());
					}
					break;
					case TokenType.GOSUB:
					{
						toker.NextType();
						result = new GosubNode(parseIdent());
					}
					break;
					case TokenType.RETURN:
					{
						toker.NextType();
						result = new ReturnNode(parseExpr(true));
					}
					break;
					case TokenType.DELETE:
					{
						if(toker.NextType() == TokenType.EACH)
						{
							toker.NextType();
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
					case TokenType.INSERT:
					{
						toker.NextType();
						ExprNode expr1 = parseExpr(false);
						bool before = toker.TakeType() == TokenType.BEFORE;//If not Keyword.BEFORE, then Keyword.AFTER
						ExprNode expr2 = parseExpr(false);
						result = new InsertNode(expr1, expr2, before);
					}
					break;
					case TokenType.READ:
						do
						{
							toker.NextType();
							VarNode var = parseVar();
							StmtNode stmt = new ReadNode(var);
							stmts.Add(stmt);
						} while(toker.CurrType == TokenType.COMMA);
						break;
					case TokenType.RESTORE:
						if(toker.NextType() == TokenType.IDENT)
						{
							result = new RestoreNode(toker.TakeText());
						}
						else
						{
							result = new RestoreNode("");
						}
						break;
					case TokenType.DATA:
						do
						{
							toker.NextType();
							ExprNode expr = parseExpr(false);
							datas.Add(new DataDeclNode(expr, lastLabel));
						} while(toker.CurrType == TokenType.COMMA);
						break;
					case TokenType.TYPE:
						toker.NextType();
						structs.Add(parseStructDecl());
						break;
					case TokenType.CONST:
						do
						{
							toker.NextType();
							consts.Add(parseVarDecl(DECL.GLOBAL, true));
						} while(toker.CurrType == TokenType.COMMA);
						break;
					case TokenType.FUNCTION:
						toker.NextType();
						funcs.Add(parseFuncDecl());
						break;
					case TokenType.DIM:
						do
						{
							toker.NextType();
							StmtNode stmt = parseArrayDecl();
							stmts.Add(stmt);
						} while(toker.CurrType == TokenType.COMMA);
						break;
					case TokenType.LOCAL:
						do
						{
							toker.NextType();
							DeclNode d = parseVarDecl(DECL.LOCAL, false);
							StmtNode stmt = new DeclStmtNode(d);
							stmts.Add(stmt);
						} while(toker.CurrType == TokenType.COMMA);
						break;
					case TokenType.GLOBAL:
						do
						{
							toker.NextType();
							DeclNode d = parseVarDecl(DECL.GLOBAL, false);
							StmtNode stmt = new DeclStmtNode(d);
							stmts.Add(stmt);
						} while(toker.CurrType == TokenType.COMMA);
						break;
					case (TokenType)'.':
					{
						toker.NextType();
						string t = parseIdent();
						result = new LabelNode(t);
						lastLabel = t;
					}
					break;
					default:
						return stmts;
				}

				if(result!=null)
				{
					stmts.Add(result);
				}
				if(lastLabel!=null)
				{
					if(currKeyWord!=TokenType.DATA && currKeyWord!=(TokenType)'.')
					{
						lastLabel = null;
					}
				}
			}
		}

		private Ex ex(string s) => new Ex(s){file=incfile};
		private Ex exp(string s) => toker.CurrType switch
		{
			TokenType.NEXT => ex("'Next' without 'For'"),
			TokenType.WEND => ex("'Wend' without 'While'"),
			TokenType.ELSE => ex("'Else' without 'If'"),
			TokenType.ELSEIF => ex("'Elseif' without 'If'"),
			TokenType.ENDIF => ex("'Endif' without 'If'"),
			TokenType.END_FUNCTION => ex("'End Function' without 'Function'"),
			TokenType.UNTIL => ex("'Until' without 'Repeat'"),
			TokenType.FOREVER => ex("'Forever' without 'Repeat'"),
			TokenType.CASE => ex("'Case' without 'Select'"),
			TokenType.END_SELECT => ex("'End Select' without 'Select'"),
			_ => ex("Expecting " + s),
		};

		private string parseIdent()
		{
			if(toker.CurrType != TokenType.IDENT)
			{
				throw exp("identifier");
			}
			return toker.TakeText();
		}

		private string parseTypeTag()
		{
			switch(toker.CurrType)
			{
				case (TokenType)'%': toker.NextType(); return "%";
				case (TokenType)'#': toker.NextType(); return "#";
				case (TokenType)'$': toker.NextType(); return "$";
				case (TokenType)'.': toker.NextType(); return parseIdent();
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
			if(toker.CurrType == TokenType.ParenOpen)
			{
				toker.NextType();
				ExprSeqNode exprs = parseExprSeq();
				toker.AssertSkip(TokenType.ParenClose, exp, "')'");
				var = new ArrayVarNode(ident, tag, exprs);
			}
			else var = new IdentVarNode(ident, tag);

			for(;;)
			{
				if(toker.CurrType == TokenType.Backslash)
				{
					toker.NextType();
					string ident2 = parseIdent();
					string tag2 = parseTypeTag();
					ExprNode expr = new VarExprNode(var);
					var = new FieldVarNode(expr, ident2, tag2);
				}
				else if(toker.CurrType == TokenType.BracketOpen)
				{
					toker.NextType();
					ExprSeqNode exprs = parseExprSeq();
					if(exprs.exprs.Count != 1 || toker.CurrType != TokenType.BracketClose)
					{
						throw exp("']'");
					}
					toker.NextType();
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
			StmtSeqNode stmts = null;
			StmtSeqNode elseOpt = null;

			expr = parseExpr(false);
			toker.TrySkip(TokenType.THEN);

			bool blkif = isTerm(toker.CurrType);
			stmts = parseStmtSeq(blkif ? STMTS.BLOCK : STMTS.LINE);

			if(toker.TrySkip(TokenType.ELSEIF))
			{
				IfNode ifnode = parseIf();
				elseOpt = new StmtSeqNode(incfile);
				elseOpt.Add(ifnode);
			}
			else if(toker.TrySkip(TokenType.ELSE))
			{
				elseOpt = parseStmtSeq(blkif ? STMTS.BLOCK : STMTS.LINE);
			}
			IfNode ret = new IfNode(expr, stmts, elseOpt);
			if(blkif)
			{
				toker.AssertCurr(TokenType.ENDIF, exp, "'EndIf'");
			}
			else
			{
				toker.AssertCurr(TokenType.NEWLINE, exp, "end-of-line");
			}

			return ret;
		}

		private DeclNode parseVarDecl(DECL kind, bool constant)
		{
			string ident = parseIdent();
			string tag = parseTypeTag();
			DeclNode d;
			if(toker.TrySkip(TokenType.BracketOpen))
			{
				if(constant)
				{
					throw ex("Blitz arrays may not be constant");
				}
				ExprSeqNode exprs = parseExprSeq();
				if(exprs.Count != 1 || toker.CurrType != TokenType.BracketClose)
				{
					throw exp("']'");
				}
				toker.NextType();
				d = new VectorDeclNode(ident, tag, exprs, kind);
			}
			else
			{
				ExprNode expr = null;
				if(toker.CurrType == TokenType.EQ)
				{
					toker.NextType();
					expr = parseExpr(false);
				}
				else if(constant)
				{
					throw ex("Constants must be initialized");
				}
				d = new VarDeclNode(ident, tag, kind, constant, expr);
			}
			d.file = incfile;
			return d;
		}
		private DimNode parseArrayDecl()
		{
			string ident = parseIdent();
			string tag = parseTypeTag();
			toker.AssertSkip(TokenType.ParenOpen, exp, "'('");
			ExprSeqNode exprs = parseExprSeq();
			toker.AssertSkip(TokenType.ParenClose, exp, "')'");
			if(exprs.Count==0)
			{
				throw ex("can't have a 0 dimensional array");
			}
			DimNode d = new DimNode(ident, tag, exprs);
			arrayDecls[ident] = d;
			return d;
		}
		private DeclNode parseFuncDecl()
		{
			string ident = parseIdent();
			string tag = parseTypeTag();

			DeclSeqNode @params = new DeclSeqNode();
			toker.AssertSkip(TokenType.ParenOpen, exp, "'('");
			if(!toker.TrySkip(TokenType.ParenClose))
			{
				for(;;)
				{
					@params.Add(parseVarDecl(DECL.PARAM, false));
					if(toker.CurrType != TokenType.COMMA) break;
					toker.NextType();
				}
				toker.AssertSkip(TokenType.ParenClose, exp, "')'");
			}

			StmtSeqNode stmts = parseStmtSeq(STMTS.BLOCK);
			if(toker.CurrType != TokenType.END_FUNCTION)
			{
				throw exp("'End Function'");
			}
			StmtNode ret = new ReturnNode(null);
			stmts.Add(ret);
			toker.NextType();
			DeclNode d = new FuncDeclNode(ident, tag, @params, stmts);
			d.file = incfile;
			return d;
		}
		private DeclNode parseStructDecl()
		{
			string ident = parseIdent();
			DeclSeqNode fields = new DeclSeqNode();
			while(toker.CurrType == TokenType.NEWLINE)
			{
				fields.AddComment(toker.TakeText());
			}
			while(toker.TrySkip(TokenType.FIELD))
			{
				do
				{
					fields.Add(parseVarDecl(DECL.FIELD, false));
				}
				while(toker.TrySkip(TokenType.COMMA));
				
				while(toker.CurrType == TokenType.NEWLINE)
				{
					fields.AddComment(toker.TakeText());
				}
			}
			toker.AssertSkip(TokenType.END_TYPE, exp, "'Field' or 'End Type'");
			DeclNode d = new StructDeclNode(ident, fields);
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
				if(!toker.TrySkip(TokenType.COMMA))
				{
					break;
				}
				opt = false;
			}
			return exprs;
		}

		private ExprNode parseExpr(bool opt)
		{
			if(toker.TrySkip(TokenType.NOT))
			{
				ExprNode expr = parseExpr1(false);
				return new RelExprNode(TokenType.EQ, expr, new IntConstNode("0"));
			}
			return parseExpr1(opt);
		}
		private ExprNode parseExpr1(bool opt) //And, Or, Eor
		{
			ExprNode lhs = parseExpr2(opt);
			if(lhs is null) return null;

			while(toker.TryTake(out TokenType c, TokenType.AND, TokenType.OR, TokenType.XOR))
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

			while(toker.TryTake(out TokenType c, TokenType.LT, TokenType.GT, TokenType.EQ, TokenType.LE, TokenType.GE, TokenType.NE))
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

			while(toker.TryTake(out TokenType c, TokenType.ADD, TokenType.SUB))
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

			while(toker.TryTake(out TokenType c, TokenType.SHL, TokenType.SHR, TokenType.SAR))
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
			while(toker.TryTake(out TokenType c, TokenType.MUL, TokenType.DIV, TokenType.MOD))
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
			while(toker.TryTake(out TokenType c, TokenType.POW))
			{
				ExprNode rhs = parseUniExpr(false);
				lhs = new ArithExprNode(c, lhs, rhs);
			}
			return lhs;
		}

		private ExprNode parseUniExpr(bool opt) //+,-,Not,~
		{
			ExprNode result;

			TokenType c = toker.CurrType;
			switch(c)
			{
				case TokenType.INT:
					toker.NextType();
					toker.TrySkip((TokenType)'%');
					result = parseUniExpr(false);
					result = new CastNode(result, Type.Int);
					break;
				case TokenType.FLOAT:
					toker.NextType();
					toker.TrySkip((TokenType)'#');
					result = parseUniExpr(false);
					result = new CastNode(result, Type.Float);
					break;
				case TokenType.STR:
					toker.NextType();
					toker.TrySkip((TokenType)'$');
					result = parseUniExpr(false);
					result = new CastNode(result, Type.String);
					break;
				case TokenType.OBJECT:
					toker.NextType();
					toker.TrySkip((TokenType)'.');
					string t = parseIdent();
					result = parseUniExpr(false);
					result = new ObjectCastNode(result, t);
					break;
				case TokenType.HANDLE:
					toker.NextType();
					result = parseUniExpr(false);
					result = new ObjectHandleNode(result);
					break;
				case TokenType.BEFORE:
					toker.NextType();
					result = parseUniExpr(false);
					result = new BeforeNode(result);
					break;
				case TokenType.AFTER:
					toker.NextType();
					result = parseUniExpr(false);
					result = new AfterNode(result);
					break;
				case TokenType.POSITIVE:
				case TokenType.NEGATIVE:
				case TokenType.BITNOT:
				case TokenType.ABS:
				case TokenType.SGN:
					toker.NextType();
					result = parseUniExpr(false);
					if(c == TokenType.BITNOT)
					{
						//TODO: Make this unary again
						result = new BinExprNode(TokenType.XOR, result, new IntConstNode("-1"));
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
			switch(toker.CurrType)
			{
				case TokenType.ParenOpen:
				{
					toker.NextType();
					ExprNode expr = parseExpr(false);
					toker.AssertSkip(TokenType.ParenClose, exp, "')'");
					return expr;
				}
				case TokenType.NEW:
				{
					toker.NextType();
					return new NewNode(parseIdent());
				}
				case TokenType.FIRST:
				{
					toker.NextType();
					return new FirstNode(parseIdent());
				}
				case TokenType.LAST:
				{
					toker.NextType();
					return new LastNode(parseIdent());
				}
				case TokenType.NULL:
					toker.NextType();
					return new NullNode();
				case TokenType.INTCONST:
				{
					return new IntConstNode(toker.TakeText());
				}
				case TokenType.FLOATCONST:
				{
					return new FloatConstNode(toker.TakeText());
				}
				case TokenType.STRINGCONST:
				{
					return new StringConstNode(toker.TakeText());
				}
				case TokenType.BINCONST:
				{
					return new IntConstNode(toker.TakeText());
				}
				case TokenType.HEXCONST:
				{
					return new IntConstNode(toker.TakeText());
				}
				case TokenType.PI:
					toker.NextType();
					return new FloatConstNode("MathF.PI" /*3.1415926535897932384626433832795f*/);
				case TokenType.TRUE:
					toker.NextType();
					return new IntConstNode("1");
				case TokenType.FALSE:
					toker.NextType();
					return new IntConstNode("0");
				case TokenType.IDENT:
					string ident = toker.TakeText();
					string tag = parseTypeTag();
					if(toker.CurrType == TokenType.ParenOpen && !arrayDecls.ContainsKey(ident))
					{
						//must be a func
						toker.NextType();
						ExprSeqNode exprs = parseExprSeq();
						toker.AssertSkip(TokenType.ParenClose, exp, "')'");
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