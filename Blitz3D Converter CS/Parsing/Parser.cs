using System;
using System.Collections.Generic;
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

		private readonly Dictionary<string,FileNode> included = new Dictionary<string,FileNode>();
		public IReadOnlyDictionary<string,FileNode> Included => included;

		private Tokenizer toker;
		private readonly Dictionary<string, DimNode> arrayDecls = new Dictionary<string, DimNode>(StringComparer.OrdinalIgnoreCase);

		private DeclSeqNode consts;
		private DeclSeqNode structs;
		private DeclSeqNode funcs;
		private DeclSeqNode datas;

		private static bool IsTerm(TokenType c) => c == (TokenType)':' || c == TokenType.NEWLINE;

		public Parser(Tokenizer t)
		{
			toker = t;
		}

		public ProgNode Parse()
		{
			consts = new DeclSeqNode();
			structs = new DeclSeqNode();
			funcs = new DeclSeqNode();
			datas = new DeclSeqNode();

			StmtSeqNode stmts = ParseStmtSeq(STMTS.PROG);	
			
			if(toker.CurrType!=TokenType.EOF)
			{
				throw Exp("end-of-file");
			}

			return new ProgNode(consts, structs, funcs, datas, stmts);
		}

		private StmtSeqNode ParseStmtSeq(STMTS scope)
		{
			StmtSeqNode stmts = new StmtSeqNode(toker.InputFile);
			string lastLabel = null;
			while(true)
			{
				while(toker.CurrType == (TokenType)':' || (scope != STMTS.LINE && toker.CurrType == TokenType.NEWLINE))
				{
					var token = toker.Take();
					if(token.Type == TokenType.NEWLINE)
					{
						stmts.AddComment(token.Text);
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

							Tokenizer i_toker = new Tokenizer(i_stream, inc);

							Tokenizer t_toker = toker;
							toker = i_toker;

							include = new FileNode(includeClassPath);
							included.Add(i_toker.InputFile, include);
							
							//Assign stmts after adding to dictionary so we know that it already exists.
							include.stmts = ParseStmtSeq(scope);

							if(toker.CurrType!=TokenType.EOF)
							{
								throw Exp("end-of-file");
							}

							toker = t_toker;
						}
						result = new IncludeNode(include);
					}
					break;
					case TokenType.IDENT:
					{
						string ident = toker.TakeText();
						string tag = ParseTypeTag();
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
									if(IsTerm(c))
									{
										throw Ex("Mismatched brackets");
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
								if(IsTerm(toker.LookAhead(k)))
								{
									toker.NextType();
									exprs = ParseExprSeq();
									toker.AssertSkip(TokenType.ParenClose, Exp,"')'");
								}
								else
								{
									exprs = ParseExprSeq();
								}
							}
							else
							{
								exprs = ParseExprSeq();
							}
							CallNode call = new CallNode(ident, tag, exprs);
							result = new ExprStmtNode(call);
						}
						else
						{
							//must be a var
							VarNode var = ParseVar(ident, tag);
							toker.AssertSkip(TokenType.EQ, Exp, "variable assignment");
							ExprNode expr = ParseExpr(false);
							result = new AsgnNode(var, expr);
						}
					}
					break;
					case TokenType.IF:
					{
						toker.NextType();
						result = ParseIf();
						toker.TrySkip(TokenType.ENDIF);
					}
					break;
					case TokenType.WHILE:
					{
						toker.NextType();
						ExprNode expr = ParseExpr(false);
						StmtSeqNode stmts2 = ParseStmtSeq(STMTS.BLOCK);
						toker.AssertSkip(TokenType.WEND, Exp, "'Wend'");
						result = new WhileNode(expr, stmts2);
					}
					break;
					case TokenType.REPEAT:
					{
						toker.NextType();
						ExprNode expr = null;
						StmtSeqNode stmts2 = ParseStmtSeq(STMTS.BLOCK);
						TokenType curr = toker.CurrType;
						if(curr != TokenType.UNTIL && curr != TokenType.FOREVER)
						{
							throw Exp("'Until' or 'Forever'");
						}
						toker.NextType();
						if(curr == TokenType.UNTIL)
						{
							expr = ParseExpr(false);
						}
						result = new RepeatNode(stmts2, expr);
					}
					break;
					case TokenType.SELECT:
					{
						toker.NextType();
						ExprNode expr = ParseExpr(false);
						SelectNode selNode = new SelectNode(expr);
						while(!toker.TrySkip(TokenType.END_SELECT))
						{
							toker.SkipWhile(IsTerm);
							if(toker.TrySkip(TokenType.CASE))
							{
								ExprSeqNode exprs = ParseExprSeq();
								StmtSeqNode stmts2 = ParseStmtSeq(STMTS.BLOCK);
								selNode.Add(new CaseNode(exprs, stmts2));
							}
							else if(toker.TrySkip(TokenType.DEFAULT))
							{
								selNode.Add(new DefaultCaseNode(ParseStmtSeq(STMTS.BLOCK)));
							}
							else
							{
								throw Exp("'Case', 'Default' or 'End Select'");
							}
						}
						result = selNode;
					}
					break;
					case TokenType.FOR:
					{
						toker.NextType();
						VarNode var = ParseVar();
						toker.AssertSkip(TokenType.EQ, Exp, "variable assignment");
						if(toker.TrySkip(TokenType.EACH))
						{
							string ident = ParseIdent();
							StmtSeqNode stmts2 = ParseStmtSeq(STMTS.BLOCK);
							toker.AssertSkip(TokenType.NEXT, Exp, "'Next'");
							result = new ForEachNode(var, ident, stmts2);
						}
						else
						{
							ExprNode from, to, step;
							from = ParseExpr(false);
							toker.AssertSkip(TokenType.TO, Exp, "'TO'");
							to = ParseExpr(false);
							//step...
							if(toker.CurrType == TokenType.STEP)
							{
								toker.NextType();
								step = ParseExpr(false);
							}
							else step = new IntConstNode("1");
							StmtSeqNode stmts2 = ParseStmtSeq(STMTS.BLOCK);
							toker.AssertSkip(TokenType.NEXT, Exp, "'Next'");
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
						result = new GotoNode(ParseIdent());
					}
					break;
					case TokenType.GOSUB:
					{
						toker.NextType();
						result = new GosubNode(ParseIdent());
					}
					break;
					case TokenType.RETURN:
					{
						toker.NextType();
						result = new ReturnNode(ParseExpr(true));
					}
					break;
					case TokenType.DELETE:
					{
						if(toker.NextType() == TokenType.EACH)
						{
							toker.NextType();
							string t = ParseIdent();
							result = new DeleteEachNode(t);
						}
						else
						{
							ExprNode expr = ParseExpr(false);
							result = new DeleteNode(expr);
						}
					}
					break;
					case TokenType.INSERT:
					{
						toker.NextType();
						ExprNode expr1 = ParseExpr(false);
						bool before = toker.TakeType() == TokenType.BEFORE;//If not Keyword.BEFORE, then Keyword.AFTER
						ExprNode expr2 = ParseExpr(false);
						result = new InsertNode(expr1, expr2, before);
					}
					break;
					case TokenType.READ:
						do
						{
							toker.NextType();
							VarNode var = ParseVar();
							StmtNode stmt = new ReadNode(var);
							stmts.Add(stmt);
						}
						while(toker.CurrType == TokenType.COMMA);
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
							ExprNode expr = ParseExpr(false);
							datas.Add(new DataDeclNode(expr, lastLabel));
						}
						while(toker.CurrType == TokenType.COMMA);
						break;
					case TokenType.TYPE:
						toker.NextType();
						structs.Add(ParseStructDecl());
						break;
					case TokenType.CONST:
						do
						{
							toker.NextType();
							consts.Add(ParseVarDecl(DeclKind.Global, true));
						}
						while(toker.CurrType == TokenType.COMMA);
						break;
					case TokenType.FUNCTION:
						toker.NextType();
						funcs.Add(ParseFuncDecl());
						break;
					case TokenType.DIM:
						do
						{
							toker.NextType();
							StmtNode stmt = ParseArrayDecl();
							stmts.Add(stmt);
						}
						while(toker.CurrType == TokenType.COMMA);
						break;
					case TokenType.LOCAL:
						do
						{
							toker.NextType();
							DeclNode d = ParseVarDecl(DeclKind.Local, false);
							StmtNode stmt = new DeclStmtNode(d);
							stmts.Add(stmt);
						}
						while(toker.CurrType == TokenType.COMMA);
						break;
					case TokenType.GLOBAL:
						do
						{
							toker.NextType();
							DeclNode d = ParseVarDecl(DeclKind.Global, false);
							StmtNode stmt = new DeclStmtNode(d);
							stmts.Add(stmt);
						}
						while(toker.CurrType == TokenType.COMMA);
						break;
					case (TokenType)'.':
					{
						toker.NextType();
						string t = ParseIdent();
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

		private Ex Ex(string s) => new Ex(s, toker.InputFile);
		private Ex Exp(string s) => Ex(toker.CurrType switch
		{
			TokenType.NEXT => "'Next' without 'For'",
			TokenType.WEND => "'Wend' without 'While'",
			TokenType.ELSE => "'Else' without 'If'",
			TokenType.ELSEIF => "'Elseif' without 'If'",
			TokenType.ENDIF => "'Endif' without 'If'",
			TokenType.END_FUNCTION => "'End Function' without 'Function'",
			TokenType.UNTIL => "'Until' without 'Repeat'",
			TokenType.FOREVER => "'Forever' without 'Repeat'",
			TokenType.CASE => "'Case' without 'Select'",
			TokenType.END_SELECT => "'End Select' without 'Select'",
			_ => "Expecting " + s,
		});

		private string ParseIdent()
		{
			if(toker.CurrType != TokenType.IDENT)
			{
				throw Exp("identifier");
			}
			return toker.TakeText();
		}

		private string ParseTypeTag()
		{
			switch(toker.CurrType)
			{
				case (TokenType)'%': toker.NextType(); return "%";
				case (TokenType)'#': toker.NextType(); return "#";
				case (TokenType)'$': toker.NextType(); return "$";
				case (TokenType)'.': toker.NextType(); return ParseIdent();
				default: return "";
			}
		}

		private bool TryParseComment(out string text)
		{
			if(toker.CurrType == TokenType.NEWLINE && !string.IsNullOrEmpty(toker.CurrText))
			{
				text = toker.TakeText();
				return true;
			}
			text = null;
			return false;
		}

		private VarNode ParseVar()
		{
			string ident = ParseIdent();
			string tag = ParseTypeTag();
			return ParseVar(ident, tag);
		}
		private VarNode ParseVar(string ident, string tag)
		{
			VarNode var;
			if(toker.CurrType == TokenType.ParenOpen)
			{
				toker.NextType();
				ExprSeqNode exprs = ParseExprSeq();
				toker.AssertSkip(TokenType.ParenClose, Exp, "')'");
				var = new ArrayVarNode(ident, tag, exprs);
			}
			else
			{
				var = new IdentVarNode(ident, tag);
			}

			while(true)
			{
				if(toker.CurrType == TokenType.Backslash)
				{
					toker.NextType();
					string ident2 = ParseIdent();
					string tag2 = ParseTypeTag();
					ExprNode expr = new VarExprNode(var);
					var = new FieldVarNode(expr, ident2, tag2);
				}
				else if(toker.CurrType == TokenType.BracketOpen)
				{
					toker.NextType();
					ExprSeqNode exprs = ParseExprSeq();
					if(exprs.Exprs.Count != 1 || toker.CurrType != TokenType.BracketClose)
					{
						throw Exp("']'");
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

		private IfNode ParseIf()
		{
			ExprNode expr = ParseExpr(false);
			toker.TrySkip(TokenType.THEN);
			
			bool blockIf = IsTerm(toker.CurrType);
			var hasComment = TryParseComment(out var comment);

			StmtSeqNode stmts = ParseStmtSeq(blockIf ? STMTS.BLOCK : STMTS.LINE);

			StmtSeqNode elseOpt = null;
			if(toker.TrySkip(TokenType.ELSEIF))
			{
				IfNode ifnode = ParseIf();
				elseOpt = new StmtSeqNode(toker.InputFile);
				elseOpt.Add(ifnode);
			}
			else if(toker.TrySkip(TokenType.ELSE))
			{
				elseOpt = ParseStmtSeq(blockIf ? STMTS.BLOCK : STMTS.LINE);
			}
			IfNode ret = new IfNode(expr, stmts, elseOpt)
			{
				Comment = comment
			};
			if(blockIf)
			{
				toker.AssertCurr(TokenType.ENDIF, Exp, "'EndIf'");
			}
			else
			{
				toker.AssertCurr(TokenType.NEWLINE, Exp, "end-of-line");
			}

			return ret;
		}

		private DeclNode ParseVarDecl(DeclKind kind, bool constant)
		{
			string ident = ParseIdent();
			string tag = ParseTypeTag();
			DeclNode d;
			if(toker.TrySkip(TokenType.BracketOpen))
			{
				if(constant)
				{
					throw Ex("Blitz arrays may not be constant");
				}
				ExprSeqNode exprs = ParseExprSeq();
				if(exprs.Count != 1 || toker.CurrType != TokenType.BracketClose)
				{
					throw Exp("']'");
				}
				toker.NextType();
				d = new VectorDeclNode(ident, tag, exprs, kind)
				{
					File = toker.InputFile
				};
			}
			else
			{
				ExprNode expr = null;
				if(toker.CurrType == TokenType.EQ)
				{
					toker.NextType();
					expr = ParseExpr(false);
				}
				else if(constant)
				{
					throw Ex("Constants must be initialized");
				}
				d = new VarDeclNode(ident, tag, kind, constant, expr)
				{
					File = toker.InputFile
				};
			}
			return d;
		}
		private DimNode ParseArrayDecl()
		{
			string ident = ParseIdent();
			string tag = ParseTypeTag();
			toker.AssertSkip(TokenType.ParenOpen, Exp, "'('");
			ExprSeqNode exprs = ParseExprSeq();
			toker.AssertSkip(TokenType.ParenClose, Exp, "')'");
			if(exprs.Count==0)
			{
				throw Ex("can't have a 0 dimensional array");
			}
			DimNode d = new DimNode(ident, tag, exprs);
			arrayDecls[ident] = d;
			return d;
		}
		private DeclNode ParseFuncDecl()
		{
			string ident = ParseIdent();
			string tag = ParseTypeTag();

			DeclSeqNode @params = new DeclSeqNode();
			toker.AssertSkip(TokenType.ParenOpen, Exp, "'('");
			if(!toker.TrySkip(TokenType.ParenClose))
			{
				while(true)
				{
					@params.Add(ParseVarDecl(DeclKind.Param, false));
					if(toker.CurrType != TokenType.COMMA) break;
					toker.NextType();
				}
				toker.AssertSkip(TokenType.ParenClose, Exp, "')'");
			}

			StmtSeqNode stmts = ParseStmtSeq(STMTS.BLOCK);
			if(toker.CurrType != TokenType.END_FUNCTION)
			{
				throw Exp("'End Function'");
			}
			stmts.Add(new ReturnNode(null));
			toker.NextType();
			var d = new FuncDeclNode(ident, tag, @params, stmts)
			{
				File = toker.InputFile
			};
			return d;
		}
		private DeclNode ParseStructDecl()
		{
			string ident = ParseIdent();
			DeclSeqNode fields = new DeclSeqNode();
			while(toker.CurrType == TokenType.NEWLINE)
			{
				fields.AddComment(toker.TakeText());
			}
			while(toker.TrySkip(TokenType.FIELD))
			{
				do
				{
					fields.Add(ParseVarDecl(DeclKind.Field, false));
				}
				while(toker.TrySkip(TokenType.COMMA));
				
				while(toker.CurrType == TokenType.NEWLINE)
				{
					fields.AddComment(toker.TakeText());
				}
			}
			toker.AssertSkip(TokenType.END_TYPE, Exp, "'Field' or 'End Type'");
			var d = new StructDeclNode(ident, fields)
			{
				File = toker.InputFile
			};
			return d;
		}

		private ExprSeqNode ParseExprSeq()
		{
			ExprSeqNode exprs = new ExprSeqNode();
			bool opt = true;
			while(ParseExpr(opt) is ExprNode e)
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

		private ExprNode ParseExpr(bool opt)
		{
			if(toker.TrySkip(TokenType.NOT))
			{
				ExprNode expr = ParseExpr1(false);
				return new UniExprNode(TokenType.NOT, expr);//return new RelExprNode(TokenType.EQ, expr, new IntConstNode("0"));
			}
			return ParseExpr1(opt);
		}
		private ExprNode ParseExpr1(bool opt) //And, Or, Eor
		{
			ExprNode lhs = ParseExpr2(opt);
			if(lhs is null) return null;

			while(toker.TryTake(out TokenType c, TokenType.AND, TokenType.OR, TokenType.XOR))
			{
				ExprNode rhs = ParseExpr2(false);
				lhs = new BinExprNode(c, lhs, rhs);
			}
			return lhs;
		}
		private ExprNode ParseExpr2(bool opt) //<,=,>,<=,<>,>=
		{
			ExprNode lhs = ParseExpr3(opt);
			if(lhs is null) return null;

			while(toker.TryTake(out TokenType c, TokenType.LT, TokenType.GT, TokenType.EQ, TokenType.LE, TokenType.GE, TokenType.NE))
			{
				ExprNode rhs = ParseExpr3(false);
				lhs = new RelExprNode(c, lhs, rhs);
			}
			return lhs;
		}
		private ExprNode ParseExpr3(bool opt) //+,-
		{
			ExprNode lhs = ParseExpr4(opt);
			if(lhs is null) return null;

			while(toker.TryTake(out TokenType c, TokenType.ADD, TokenType.SUB))
			{
				ExprNode rhs = ParseExpr4(false);
				lhs = new ArithExprNode(c, lhs, rhs);
			}
			return lhs;
		}
		private ExprNode ParseExpr4(bool opt) //Lsr,Lsr,Asr
		{
			ExprNode lhs = ParseExpr5(opt);
			if(lhs is null) return null;

			while(toker.TryTake(out TokenType c, TokenType.SHL, TokenType.SHR, TokenType.SAR))
			{
				ExprNode rhs = ParseExpr5(false);
				lhs = new BinExprNode(c, lhs, rhs);
			}
			return lhs;
		}
		private ExprNode ParseExpr5(bool opt) //*,/,Mod
		{
			ExprNode lhs = ParseExpr6(opt);
			if(lhs is null) return null;
			while(toker.TryTake(out TokenType c, TokenType.MUL, TokenType.DIV, TokenType.MOD))
			{
				ExprNode rhs = ParseExpr6(false);
				lhs = new ArithExprNode(c, lhs, rhs);
			}
			return lhs;
		}
		private ExprNode ParseExpr6(bool opt) //^
		{
			ExprNode lhs = ParseUniExpr(opt);
			if(lhs is null) return null;
			while(toker.TryTake(out TokenType c, TokenType.POW))
			{
				ExprNode rhs = ParseUniExpr(false);
				lhs = new ArithExprNode(c, lhs, rhs);
			}
			return lhs;
		}

		private ExprNode ParseUniExpr(bool opt) //+,-,Not,~
		{
			TokenType c = toker.CurrType;
			switch(c)
			{
				case TokenType.INT:
				{
					toker.NextType();
					toker.TrySkip((TokenType)'%');
					ExprNode result = ParseUniExpr(false);
					return new CastNode(result, Type.Int);
				}
				case TokenType.FLOAT:
				{
					toker.NextType();
					toker.TrySkip((TokenType)'#');
					ExprNode result = ParseUniExpr(false);
					return new CastNode(result, Type.Float);
				}
				case TokenType.STR:
				{
					toker.NextType();
					toker.TrySkip((TokenType)'$');
					ExprNode result = ParseUniExpr(false);
					return new CastNode(result, Type.String);
				}
				case TokenType.OBJECT:
				{
					toker.NextType();
					toker.TrySkip((TokenType)'.');
					string t = ParseIdent();
					ExprNode result = ParseUniExpr(false);
					return new ObjectCastNode(result, t);
				}
				case TokenType.HANDLE:
				{
					toker.NextType();
					ExprNode result = ParseUniExpr(false);
					return new ObjectHandleNode(result);
				}
				case TokenType.BEFORE:
				{
					toker.NextType();
					ExprNode result = ParseUniExpr(false);
					return new BeforeNode(result);
				}
				case TokenType.AFTER:
				{
					toker.NextType();
					ExprNode result = ParseUniExpr(false);
					return new AfterNode(result);
				}
				case TokenType.POSITIVE:
				case TokenType.NEGATIVE:
				case TokenType.BITNOT:
				case TokenType.ABS:
				case TokenType.SGN:
				{
					toker.NextType();
					ExprNode result = ParseUniExpr(false);
					return new UniExprNode(c, result);
				}
				default:
				{
					return ParsePrimary(opt);
				}
			}
		}
		private ExprNode ParsePrimary(bool opt)
		{
			switch(toker.CurrType)
			{
				case TokenType.ParenOpen:
				{
					toker.NextType();
					ExprNode expr = ParseExpr(false);
					toker.AssertSkip(TokenType.ParenClose, Exp, "')'");
					return expr;
				}
				case TokenType.NEW:
				{
					toker.NextType();
					return new NewNode(ParseIdent());
				}
				case TokenType.FIRST:
				{
					toker.NextType();
					return new FirstNode(ParseIdent());
				}
				case TokenType.LAST:
				{
					toker.NextType();
					return new LastNode(ParseIdent());
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
					return new IntConstNode("true");
				case TokenType.FALSE:
					toker.NextType();
					return new IntConstNode("false");
				case TokenType.IDENT:
					string ident = toker.TakeText();
					string tag = ParseTypeTag();
					if(toker.CurrType == TokenType.ParenOpen && !arrayDecls.ContainsKey(ident))
					{
						//must be a func
						toker.NextType();
						ExprSeqNode exprs = ParseExprSeq();
						toker.AssertSkip(TokenType.ParenClose, Exp, "')'");
						return new CallNode(ident, tag, exprs);
					}
					else
					{
						//must be a var
						VarNode var = ParseVar(ident, tag);
						return new VarExprNode(var);
					}
				default:
					if(!opt)
					{
						throw Exp("expression");
					}
					return null;
			}
		}
	}
}