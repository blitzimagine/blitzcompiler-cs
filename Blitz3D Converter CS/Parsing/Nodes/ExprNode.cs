using System;
using System.Collections.Generic;
using System.Linq;

namespace Blitz3D.Parsing.Nodes
{
	//////////////////////////////////
	// Cast an expression to a type //
	//////////////////////////////////
	public abstract class ExprNode:Node
	{
		public Type sem_type = null;
		public ExprNode() { }
		public ExprNode(Type t)
		{
			sem_type = t;
		}

		public ExprNode castTo(Type ty, Environ e)
		{
			if(!sem_type.CanCastTo(ty))
			{
				throw ex("Illegal type conversion");
			}

			ExprNode cast = new CastNode(this, ty);
			return cast.Semant(e);
		}


		public abstract ExprNode Semant(Environ e);
	}

	/////////////////////////////
	// Sequence of Expressions //
	/////////////////////////////
	public class ExprSeqNode:Node
	{
		public readonly List<ExprNode> exprs = new List<ExprNode>();

		public void Add(ExprNode e) => exprs.Add(e);

		public int Count => exprs.Count;

		public void semant(Environ e)
		{
			for(int k = 0; k < exprs.Count; ++k)
			{
				if(exprs[k]!=null) exprs[k] = exprs[k].Semant(e);
			}
		}
		//public TNode translate(Codegen g, bool userlib)
		//{
		//	TNode t = null, l = null;
		//	for(int k = 0; k < exprs.Count; ++k)
		//	{
		//		TNode q = exprs[k].Translate(g);

		//		if(userlib)
		//		{
		//			Type ty = exprs[k].sem_type;
		//			if(ty.stringType())
		//			{
		//				q = call("__bbStrToCStr", q);
		//			}
		//			else if(ty.structType()!=null)
		//			{
		//				q = new TNode(IR.MEM, q);
		//			}
		//			else if(ty == Type.void_type)
		//			{
		//				q = new TNode(IR.MEM, add(q, iconst(4)));
		//			}
		//		}

		//		TNode p;
		//		p = new TNode(IR.ARG, null, null, k * 4);
		//		p = new TNode(IR.MEM, p, null);
		//		p = new TNode(IR.MOVE, q, p);
		//		p = new TNode(IR.SEQ, p, null);
		//		if(l!=null) l.r = p;
		//		else t = p;
		//		l = p;
		//	}
		//	return t;
		//}
		public void castTo(DeclSeq decls, Environ e, bool userlib)
		{
			if(exprs.Count > decls.Count) throw ex("Too many parameters");
			for(int k = 0; k < decls.Count; ++k)
			{
				Decl d = decls.decls[k];
				if(k < exprs.Count && exprs[k]!=null)
				{
					if(userlib && d.type is StructType)
					{
						if(!(exprs[k].sem_type is StructType))
						{
							if(exprs[k].sem_type is IntType)
							{
								exprs[k].sem_type = Type.void_type;
							}
							else
							{
								throw ex("Illegal type conversion");
							}
						}
						continue;
					}

					exprs[k] = exprs[k].castTo(d.type, e);
				}
				else
				{
					if(d.defType is null)
					{
						throw ex("Not enough parameters");
					}
					ExprNode expr = constValue(d.defType);
					if(k < exprs.Count) exprs[k] = expr;
					else exprs.Add(expr);
				}
			}
		}
		public void castTo(Type t, Environ e)
		{
			for(int k = 0; k < exprs.Count; ++k)
			{
				exprs[k] = exprs[k].castTo(t, e);
			}
		}

		public override IEnumerable<string> WriteData()
		{
			yield return string.Join(", ", exprs.Select(e=>e.JoinedWriteData()));
		}
	}

	//#include "varnode.h"

	//////////////////////////////////
	// Cast an expression to a type //
	//////////////////////////////////
	public class CastNode:ExprNode
	{
		public ExprNode expr;
		public Type type;
		public CastNode(ExprNode ex, Type ty)
		{
			expr = ex;
			type = ty;
		}

		public override ExprNode Semant(Environ e)
		{
			if(expr.sem_type is null)
			{
				expr = expr.Semant(e);
			}

			if(expr is ConstNode c)
			{
				ExprNode e2;
				if(type == Type.int_type)
				{
					e2 = new IntConstNode(c.intValue());
				}
				else if(type == Type.float_type)
				{
					e2 = new FloatConstNode(c.floatValue());
				}
				else
				{
					e2 = new StringConstNode(c.stringValue());
				}
				//delete this;
				return e2;
			}

			sem_type = type;
			return this;
		}
		//public override TNode Translate(Codegen g)
		//{
		//	TNode t = expr.Translate(g);
		//	if(expr.sem_type == Type.float_type && sem_type == Type.int_type)
		//	{
		//		//float->int
		//		return new TNode(IR.CAST, t, null);
		//	}
		//	if(expr.sem_type == Type.int_type && sem_type == Type.float_type)
		//	{
		//		//int->float
		//		return new TNode(IR.FCAST, t, null);
		//	}
		//	if(expr.sem_type == Type.string_type && sem_type == Type.int_type)
		//	{
		//		//str->int
		//		return call("__bbStrToInt", t);
		//	}
		//	if(expr.sem_type == Type.int_type && sem_type == Type.string_type)
		//	{
		//		//int->str
		//		return call("__bbStrFromInt", t);
		//	}
		//	if(expr.sem_type == Type.string_type && sem_type == Type.float_type)
		//	{
		//		//str->float
		//		return fcall("__bbStrToFloat", t);
		//	}
		//	if(expr.sem_type == Type.float_type && sem_type == Type.string_type)
		//	{
		//		//float->str
		//		return call("__bbStrFromFloat", t);
		//	}
		//	if(expr.sem_type.structType()!=null && sem_type == Type.string_type)
		//	{
		//		//obj->str
		//		return call("__bbObjToStr", t);
		//	}
		//	return t;
		//}

		public override IEnumerable<string> WriteData()
		{
			if(expr.sem_type == sem_type)
			{
				return expr.WriteData();
			}
			return new[]{$"({sem_type.Name})({expr.JoinedWriteData()})"};
		}
	}

	///////////////////
	// Function call //
	///////////////////
	public class CallNode:ExprNode
	{
		public string ident, tag;
		public ExprSeqNode exprs;
		public Decl sem_decl;
		public CallNode(string i, string t, ExprSeqNode e)
		{
			ident = i;
			tag = t;
			exprs = e;
		}

		public override ExprNode Semant(Environ e)
		{
			Type t = e.findType(tag);
			sem_decl = e.findFunc(ident);
			if(sem_decl is null || (sem_decl.kind & DECL.FUNC)==0)
			{
				throw ex("Function '" + ident + "' not found");
			}
			FuncType f = (FuncType)sem_decl.type;
			if(t!=null && f.returnType != t)
			{
				throw ex("incorrect function return type");
			}
			exprs.semant(e);
			exprs.castTo(f.@params, e, f.cfunc);
			sem_type = f.returnType;
			return this;
		}
		//public override TNode Translate(Codegen g)
		//{
		//	FuncType f = sem_decl.type.funcType();

		//	TNode t;
		//	TNode l = global("_f" + ident);
		//	TNode r = exprs.translate(g, f.cfunc);

		//	if(f.userlib)
		//	{
		//		l = new TNode(IR.MEM, l);
		//		usedfuncs.Add(ident);
		//	}

		//	if(sem_type == Type.float_type)
		//	{
		//		t = new TNode(IR.FCALL, l, r, exprs.Count * 4);
		//	}
		//	else
		//	{
		//		t = new TNode(IR.CALL, l, r, exprs.Count * 4);
		//	}

		//	if(f.returnType.stringType())
		//	{
		//		if(f.cfunc)
		//		{
		//			t = call("__bbCStrToStr", t);
		//		}
		//	}
		//	return t;
		//}

		public override IEnumerable<string> WriteData()
		{
			yield return $"{ident}({exprs.JoinedWriteData()})";
		}
	}

	/////////////////////////
	// Variable expression //
	/////////////////////////
	public class VarExprNode:ExprNode
	{
		public VarNode var;
		public VarExprNode(VarNode v)
		{
			var = v;
		}

		public override ExprNode Semant(Environ e)
		{
			var.Semant(e);
			sem_type = var.sem_type;
			if(sem_type is ConstType c)
			{
				ExprNode expr = constValue(c);
				return expr;
			}
			return this;
		}

		public override IEnumerable<string> WriteData() => var.WriteData();
	}

	public abstract class ConstNode:ExprNode
	{
		public override ExprNode Semant(Environ e) => this;

		public abstract int intValue();
		public abstract float floatValue();
		public abstract string stringValue();
	}

	//////////////////////
	// Integer constant //
	//////////////////////
	public class IntConstNode:ConstNode
	{
		public int value;
		public IntConstNode(int n)
		{
			value = n;
			sem_type = Type.int_type;
		}

		public override int intValue() => value;
		public override float floatValue() => value;

		public override string stringValue() => /*itoa*/(value).ToString();

		public override IEnumerable<string> WriteData()
		{
			yield return value.ToString();
		}
	}

	////////////////////
	// Float constant //
	////////////////////
	public class FloatConstNode:ConstNode
	{
		public float value;
		public FloatConstNode(float f)
		{
			value = f;
			sem_type = Type.float_type;
		}

		public override int intValue() => (int)MathF.Round(value);
		public override float floatValue() => value;
		public override string stringValue() => value.ToString();

		public override IEnumerable<string> WriteData()
		{
			yield return value.ToString()+'f';
		}
	}

	/////////////////////
	// String constant //
	/////////////////////
	public class StringConstNode:ConstNode
	{
		private readonly string value;
		public StringConstNode(string s)
		{
			value = s;
			sem_type = Type.string_type;
		}

		public override int intValue() => int.Parse(value);
		public override float floatValue() => float.Parse(value);
		public override string stringValue() => value;

		public override IEnumerable<string> WriteData()
		{
			yield return '"'+value+'"';
		}
	}

	////////////////////
	// Unary operator //
	////////////////////
	public class UniExprNode:ExprNode
	{
		public Keyword op;
		public ExprNode expr;
		public UniExprNode(Keyword op, ExprNode expr)
		{
			this.op = op;
			this.expr = expr;
		}

		public override ExprNode Semant(Environ e)
		{
			expr = expr.Semant(e);
			sem_type = expr.sem_type;
			if(sem_type != Type.int_type && sem_type != Type.float_type) throw ex("Illegal operator for type");
			if(expr is ConstNode c)
			{
				ExprNode e2 = null;
				if(sem_type == Type.int_type)
				{
					switch(op)
					{
						case Keyword.POSITIVE:
							e2 = new IntConstNode(+c.intValue());
							break;
						case Keyword.NEGATIVE:
							e2 = new IntConstNode(-c.intValue());
							break;
						case Keyword.ABS:
							e2 = new IntConstNode(c.intValue() >= 0 ? c.intValue() : -c.intValue());
							break;
						case Keyword.SGN:
							e2 = new IntConstNode(c.intValue() > 0 ? 1 : (c.intValue() < 0 ? -1 : 0));
							break;
					}
				}
				else
				{
					switch(op)
					{
						case Keyword.POSITIVE:
							e2 = new FloatConstNode(+c.floatValue());
							break;
						case Keyword.NEGATIVE:
							e2 = new FloatConstNode(-c.floatValue());
							break;
						case Keyword.ABS:
							e2 = new FloatConstNode(c.floatValue() >= 0 ? c.floatValue() : -c.floatValue());
							break;
						case Keyword.SGN:
							e2 = new FloatConstNode(c.floatValue() > 0 ? 1 : (c.floatValue() < 0 ? -1 : 0));
							break;
					}
				}
				//delete this;
				return e2;
			}
			return this;
		}
		//public override TNode Translate(Codegen g)
		//{
		//	IR n = 0;
		//	TNode l = expr.Translate(g);
		//	if(sem_type == Type.int_type)
		//	{
		//		switch(op)
		//		{
		//			case Keyword.POSITIVE: return l;
		//			case Keyword.NEGATIVE:
		//				n = IR.NEG;
		//				break;
		//			case Keyword.ABS: return call("__bbAbs", l);
		//			case Keyword.SGN: return call("__bbSgn", l);
		//		}
		//	}
		//	else
		//	{
		//		switch(op)
		//		{
		//			case Keyword.POSITIVE: return l;
		//			case Keyword.NEGATIVE:
		//				n = IR.FNEG;
		//				break;
		//			case Keyword.ABS: return fcall("__bbFAbs", l);
		//			case Keyword.SGN: return fcall("__bbFSgn", l);
		//		}
		//	}
		//	return new TNode(n, l, null);
		//}

		public override IEnumerable<string> WriteData()
		{
			yield return op switch
			{
				Keyword.POSITIVE => $"+{expr.JoinedWriteData()}",
				Keyword.NEGATIVE => $"-{expr.JoinedWriteData()}",
				Keyword.ABS => $"Math.Abs({expr.JoinedWriteData()})",
				Keyword.SGN => $"Math.Sign({expr.JoinedWriteData()})",
				_ => throw new Exception("Invalid operation")
			};
		}
	}

	/////////////////////////////////////////////////////
	// boolean expression - accepts ints, returns ints //
	/////////////////////////////////////////////////////
	// and, or, eor, lsl, lsr, asr
	public class BinExprNode:ExprNode
	{
		public Keyword op;
		public ExprNode lhs, rhs;
		public BinExprNode(Keyword op, ExprNode lhs, ExprNode rhs)
		{
			this.op = op;
			this.lhs = lhs;
			this.rhs = rhs;
		}

		public override ExprNode Semant(Environ e)
		{
			lhs = lhs.Semant(e);
			lhs = lhs.castTo(Type.int_type, e);
			rhs = rhs.Semant(e);
			rhs = rhs.castTo(Type.int_type, e);
			if(lhs is ConstNode lc && rhs is ConstNode rc)
			{
				return op switch
				{
					Keyword.AND => new IntConstNode(lc.intValue() & rc.intValue()),
					Keyword.OR => new IntConstNode(lc.intValue() | rc.intValue()),
					Keyword.XOR => new IntConstNode(lc.intValue() ^ rc.intValue()),
					Keyword.SHL => new IntConstNode(lc.intValue() << rc.intValue()),
					Keyword.SHR => new IntConstNode((int)((uint)lc.intValue() >> rc.intValue())),
					Keyword.SAR => new IntConstNode(lc.intValue() >> rc.intValue()),
					_ => null,
				};
			}
			sem_type = Type.int_type;
			return this;
		}

		public override IEnumerable<string> WriteData()
		{
			yield return op switch
			{
				Keyword.AND => $"({lhs.JoinedWriteData()} & {rhs.JoinedWriteData()})",
				Keyword.OR => $"({lhs.JoinedWriteData()} | {rhs.JoinedWriteData()})",
				Keyword.XOR => $"({lhs.JoinedWriteData()} ^ {rhs.JoinedWriteData()})",
				Keyword.SHL => $"({lhs.JoinedWriteData()} << {rhs.JoinedWriteData()})",
				Keyword.SHR => $"(int)((uint){lhs.JoinedWriteData()} >> {rhs.JoinedWriteData()})",
				Keyword.SAR => $"({lhs.JoinedWriteData()} >> {rhs.JoinedWriteData()})",
				_ => throw new Exception("Invalid operation")
			};
		}
	}

	///////////////////////////
	// arithmetic expression //
	///////////////////////////
	// *,/,Mod,+,-
	public class ArithExprNode:ExprNode
	{
		public Keyword op;
		public ExprNode lhs, rhs;
		public ArithExprNode(Keyword op, ExprNode lhs, ExprNode rhs)
		{
			this.op = op;
			this.lhs = lhs;
			this.rhs = rhs;
		}

		public override ExprNode Semant(Environ e)
		{
			lhs = lhs.Semant(e);
			rhs = rhs.Semant(e);
			if(lhs.sem_type is StructType || rhs.sem_type is StructType)
			{
				throw ex("Arithmetic operator cannot be applied to custom type objects");
			}
			if(lhs.sem_type == Type.string_type || rhs.sem_type == Type.string_type)
			{
				//one side is a string - only + operator...
				if(op != Keyword.POSITIVE) throw ex("Operator cannot be applied to strings");
				sem_type = Type.string_type;
			}
			else if(op == Keyword.POW || lhs.sem_type == Type.float_type || rhs.sem_type == Type.float_type)
			{
				//It's ^, or one side is a float
				sem_type = Type.float_type;
			}
			else
			{
				//must be 2 ints
				sem_type = Type.int_type;
			}
			lhs = lhs.castTo(sem_type, e);
			rhs = rhs.castTo(sem_type, e);
			if(rhs is ConstNode rc)
			{
				if(op == Keyword.DIV || op == Keyword.MOD)
				{
					if((sem_type == Type.int_type && rc.intValue()==0) || (sem_type == Type.float_type && rc.floatValue()==0.0))
					{
						throw ex("Division by zero");
					}
				}
				if(lhs is ConstNode lc)
				{
					ExprNode expr = null;
					if(sem_type == Type.string_type)
					{
						expr = new StringConstNode(lc.stringValue() + rc.stringValue());
					}
					else if(sem_type == Type.int_type)
					{
						switch(op)
						{
							case Keyword.ADD:
								expr = new IntConstNode(lc.intValue() + rc.intValue());
								break;
							case Keyword.SUB:
								expr = new IntConstNode(lc.intValue() - rc.intValue());
								break;
							case Keyword.MUL:
								expr = new IntConstNode(lc.intValue() * rc.intValue());
								break;
							case Keyword.DIV:
								expr = new IntConstNode(lc.intValue() / rc.intValue());
								break;
							case Keyword.MOD:
								expr = new IntConstNode(lc.intValue() % rc.intValue());
								break;
						}
					}
					else
					{
						switch(op)
						{
							case Keyword.ADD:
								expr = new FloatConstNode(lc.floatValue() + rc.floatValue());
								break;
							case Keyword.SUB:
								expr = new FloatConstNode(lc.floatValue() - rc.floatValue());
								break;
							case Keyword.MUL:
								expr = new FloatConstNode(lc.floatValue() * rc.floatValue());
								break;
							case Keyword.DIV:
								expr = new FloatConstNode(lc.floatValue() / rc.floatValue());
								break;
							case Keyword.MOD:
								expr = new FloatConstNode(lc.floatValue() % rc.floatValue());
								break;
							case Keyword.POW:
								expr = new FloatConstNode(MathF.Pow(lc.floatValue(), rc.floatValue()));
								break;
						}
					}
					return expr;
				}
			}
			return this;
		}
		//public override TNode Translate(Codegen g)
		//{
		//	TNode l = lhs.Translate(g);
		//	TNode r = rhs.Translate(g);
		//	if(sem_type == Type.string_type)
		//	{
		//		return call("__bbStrConcat", l, r);
		//	}
		//	IR n = 0;
		//	if(sem_type == Type.int_type)
		//	{
		//		switch(op)
		//		{
		//			case Keyword.ADD:
		//				n = IR.ADD;
		//				break;
		//			case Keyword.SUB:
		//				n = IR.SUB;
		//				break;
		//			case Keyword.MUL:
		//				n = IR.MUL;
		//				break;
		//			case Keyword.DIV:
		//				n = IR.DIV;
		//				break;
		//			case Keyword.MOD: return call("__bbMod", l, r);
		//		}
		//	}
		//	else
		//	{
		//		switch(op)
		//		{
		//			case Keyword.ADD:
		//				n = IR.FADD;
		//				break;
		//			case Keyword.SUB:
		//				n = IR.FSUB;
		//				break;
		//			case Keyword.MUL:
		//				n = IR.FMUL;
		//				break;
		//			case Keyword.DIV:
		//				n = IR.FDIV;
		//				break;
		//			case Keyword.MOD: return fcall("__bbFMod", l, r);
		//			case Keyword.POW: return fcall("__bbFPow", l, r);
		//		}
		//	}
		//	return new TNode(n, l, r);
		//}

		public override IEnumerable<string> WriteData()
		{
			yield return op switch
			{
				Keyword.ADD => $"({lhs.JoinedWriteData()} + {rhs.JoinedWriteData()})",
				Keyword.SUB => $"({lhs.JoinedWriteData()} - {rhs.JoinedWriteData()})",
				Keyword.MUL => $"({lhs.JoinedWriteData()} * {rhs.JoinedWriteData()})",
				Keyword.DIV => $"({lhs.JoinedWriteData()} / {rhs.JoinedWriteData()})",
				Keyword.MOD => $"({lhs.JoinedWriteData()} % {rhs.JoinedWriteData()})",
				Keyword.POW => $"MathF.Pow({lhs.JoinedWriteData()}, {rhs.JoinedWriteData()})",
				_ => throw new Exception("Invalid operation")
			};
		}
	}

	/////////////////////////
	// relation expression //
	/////////////////////////
	//<,=,>,<=,<>,>=
	public class RelExprNode:ExprNode
	{
		public Keyword op;
		public ExprNode lhs, rhs;
		public Type opType;
		public RelExprNode(Keyword op, ExprNode lhs, ExprNode rhs)
		{
			this.op = op;
			this.lhs = lhs;
			this.rhs = rhs;
		}

		public override ExprNode Semant(Environ e)
		{
			lhs = lhs.Semant(e);
			rhs = rhs.Semant(e);
			if(lhs.sem_type is StructType || rhs.sem_type is StructType)
			{
				if(op != Keyword.EQ && op != Keyword.NE) throw ex("Illegal operator for custom type objects");
				opType = lhs.sem_type != Type.null_type ? lhs.sem_type : rhs.sem_type;
			}
			else if(lhs.sem_type == Type.string_type || rhs.sem_type == Type.string_type)
			{
				opType = Type.string_type;
			}
			else if(lhs.sem_type == Type.float_type || rhs.sem_type == Type.float_type)
			{
				opType = Type.float_type;
			}
			else
			{
				opType = Type.int_type;
			}
			sem_type = Type.int_type;
			lhs = lhs.castTo(opType, e);
			rhs = rhs.castTo(opType, e);
			ConstNode lc = lhs as ConstNode;
			ConstNode rc = rhs as ConstNode;
			if(lc!=null && rc!=null)
			{
				ExprNode expr = null;
				if(opType == Type.string_type)
				{
					switch(op)
					{
						case Keyword.LT:
							expr = new IntConstNode(lc.stringValue().CompareTo(rc.stringValue())<0 ? 1 : 0);
							break;
						case Keyword.EQ:
							expr = new IntConstNode(lc.stringValue().CompareTo(rc.stringValue()) == 0 ? 1 : 0);
							break;
						case Keyword.GT:
							expr = new IntConstNode(lc.stringValue().CompareTo(rc.stringValue()) > 0 ? 1 : 0);
							break;
						case Keyword.LE:
							expr = new IntConstNode(lc.stringValue().CompareTo(rc.stringValue()) <= 0 ? 1 : 0);
							break;
						case Keyword.NE:
							expr = new IntConstNode(lc.stringValue().CompareTo(rc.stringValue()) != 0 ? 1 : 0);
							break;
						case Keyword.GE:
							expr = new IntConstNode(lc.stringValue().CompareTo(rc.stringValue()) >= 0 ? 1 : 0);
							break;
					}
				}
				else if(opType == Type.float_type)
				{
					switch(op)
					{
						case Keyword.LT:
							expr = new IntConstNode(lc.floatValue().CompareTo(rc.floatValue()) < 0 ? 1 : 0);
							break;
						case Keyword.EQ:
							expr = new IntConstNode(lc.floatValue().CompareTo(rc.floatValue()) == 0 ? 1 : 0);
							break;
						case Keyword.GT:
							expr = new IntConstNode(lc.floatValue().CompareTo(rc.floatValue()) > 0 ? 1 : 0);
							break;
						case Keyword.LE:
							expr = new IntConstNode(lc.floatValue().CompareTo(rc.floatValue()) <= 0 ? 1 : 0);
							break;
						case Keyword.NE:
							expr = new IntConstNode(lc.floatValue().CompareTo(rc.floatValue()) != 0 ? 1 : 0);
							break;
						case Keyword.GE:
							expr = new IntConstNode(lc.floatValue().CompareTo(rc.floatValue()) >= 0 ? 1 : 0);
							break;
					}
				}
				else
				{
					switch(op)
					{
						case Keyword.LT:
							expr = new IntConstNode(lc.intValue().CompareTo(rc.intValue()) < 0 ? 1 : 0);
							break;
						case Keyword.EQ:
							expr = new IntConstNode(lc.intValue().CompareTo(rc.intValue()) == 0 ? 1 : 0);
							break;
						case Keyword.GT:
							expr = new IntConstNode(lc.intValue().CompareTo(rc.intValue()) > 0 ? 1 : 0);
							break;
						case Keyword.LE:
							expr = new IntConstNode(lc.intValue().CompareTo(rc.intValue()) <= 0 ? 1 : 0);
							break;
						case Keyword.NE:
							expr = new IntConstNode(lc.intValue().CompareTo(rc.intValue()) != 0 ? 1 : 0);
							break;
						case Keyword.GE:
							expr = new IntConstNode(lc.intValue().CompareTo(rc.intValue()) >= 0 ? 1 : 0);
							break;
					}
				}
				//delete this;
				return expr;
			}
			return this;
		}

		public override IEnumerable<string> WriteData()
		{
			if(opType == Type.string_type)//Compare strings and objects
			{
				//TODO: Make sure this matches __bbStrCompare
				yield return $"{lhs.JoinedWriteData()}.CompareTo({rhs.JoinedWriteData()})";
			}
			else if(opType is StructType)
			{
				//TODO: Add IComparable to struct by default? This would be __bbObjCompare
				yield return $"{lhs.JoinedWriteData()}.CompareTo({rhs.JoinedWriteData()})";
			}
			else
			{
				yield return op switch
				{
					Keyword.LT => $"({lhs.JoinedWriteData()} < {rhs.JoinedWriteData()})",
					Keyword.EQ => $"({lhs.JoinedWriteData()} == {rhs.JoinedWriteData()})",
					Keyword.GT => $"({lhs.JoinedWriteData()} > {rhs.JoinedWriteData()})",
					Keyword.LE => $"({lhs.JoinedWriteData()} <= {rhs.JoinedWriteData()})",
					Keyword.NE => $"({lhs.JoinedWriteData()} != {rhs.JoinedWriteData()})",
					Keyword.GE => $"({lhs.JoinedWriteData()} >= {rhs.JoinedWriteData()})",
					_ => throw new Exception("Invalid operation")
				};
			}
		}
	}

	////////////////////
	// New expression //
	////////////////////
	public class NewNode:ExprNode
	{
		public string ident;
		public NewNode(string i)
		{
			ident = i;
		}
		public override ExprNode Semant(Environ e)
		{
			sem_type = e.findType(ident);
			if(sem_type is null)
			{
				throw ex("custom type name not found");
			}
			if(!(sem_type is StructType))
			{
				throw ex("type is not a custom type");
			}
			return this;
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"new {ident}()";
		}
	}

	////////////////////
	// First of class //
	////////////////////
	public class FirstNode:ExprNode
	{
		public string ident;
		public FirstNode(string i) { ident = i; }
		public override ExprNode Semant(Environ e)
		{
			sem_type = e.findType(ident);
			if(sem_type is null) throw ex("custom type name name not found");
			return this;
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"__bbObjFirst<{ident}>()";
		}
	}

	///////////////////
	// Last of class //
	///////////////////
	public class LastNode:ExprNode
	{
		public string ident;
		public LastNode(string i)
		{
			ident = i;
		}
		public override ExprNode Semant(Environ e)
		{
			sem_type = e.findType(ident);
			if(sem_type is null) throw ex("custom type name not found");
			return this;
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"__bbObjLast<{ident}>()";
		}
	};

	////////////////////
	// Next of object //
	////////////////////
	public class AfterNode:ExprNode
	{
		public ExprNode expr;
		public AfterNode(ExprNode e)
		{
			expr = e;
		}

		public override ExprNode Semant(Environ e)
		{
			expr = expr.Semant(e);
			if(expr.sem_type == Type.null_type)
			{
				throw ex("'After' cannot be used on 'Null'");
			}
			if(!(expr.sem_type is StructType))
			{
				throw ex("'After' must be used with a custom type object");
			}
			sem_type = expr.sem_type;
			return this;
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"__bbObjNext({expr.JoinedWriteData()})";
		}
	}

	////////////////////
	// Prev of object //
	////////////////////
	public class BeforeNode:ExprNode
	{
		public ExprNode expr;
		public BeforeNode(ExprNode e)
		{
			expr = e;
		}

		public override ExprNode Semant(Environ e)
		{
			expr = expr.Semant(e);
			if(expr.sem_type == Type.null_type)
			{
				throw ex("'Before' cannot be used with 'Null'");
			}
			if(!(expr.sem_type is StructType))
			{
				throw ex("'Before' must be used with a custom type object");
			}
			sem_type = expr.sem_type;
			return this;
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"__bbObjPrev({expr.JoinedWriteData()})";
		}
	}

	/////////////////
	// Null object //
	/////////////////
	public class NullNode:ExprNode
	{
		public override ExprNode Semant(Environ e)
		{
			sem_type = Type.null_type;
			return this;
		}

		public override IEnumerable<string> WriteData()
		{
			yield return "null";
		}
	}

	/////////////////
	// Object cast //
	/////////////////
	public class ObjectCastNode:ExprNode
	{
		public ExprNode expr;
		public string type_ident;
		public ObjectCastNode(ExprNode e, string t)
		{
			expr = e;
			type_ident = t;
		}

		public override ExprNode Semant(Environ e)
		{
			expr = expr.Semant(e);
			expr = expr.castTo(Type.int_type, e);
			sem_type = e.findType(type_ident);
			if(sem_type is null) throw ex("custom type name not found");
			if(!(sem_type is StructType)) throw ex("type is not a custom type");
			return this;
		}
		//public override TNode Translate(Codegen g)
		//{
		//	TNode t = expr.Translate(g);
		//	t = call("__bbObjFromHandle", t, global("_t" + sem_type.structType().ident));
		//	return t;
		//}

		public override IEnumerable<string> WriteData()
		{
			yield return $"__bbObjFromHandle({expr.JoinedWriteData()}, {type_ident})";
		}
	}

	///////////////////
	// Object Handle //
	///////////////////
	public class ObjectHandleNode:ExprNode
	{
		public ExprNode expr;
		public ObjectHandleNode(ExprNode e) { expr = e; }

		public override ExprNode Semant(Environ e)
		{
			expr = expr.Semant(e);
			if(!(expr.sem_type is StructType)) throw ex("'ObjectHandle' must be used with an object");
			sem_type = Type.int_type;
			return this;
		}

		public override IEnumerable<string> WriteData()
		{
			yield return $"__bbObjToHandle({expr.JoinedWriteData()})";
		}
	}
}