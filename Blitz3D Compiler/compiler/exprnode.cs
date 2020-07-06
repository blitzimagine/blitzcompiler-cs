using System;
using System.Collections.Generic;

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
		if(!sem_type.canCastTo(ty))
		{
			ex("Illegal type conversion");
		}

		ExprNode cast = new CastNode(this, ty);
		return cast.semant(e);
	}


	public abstract ExprNode semant(Environ e);
	public abstract TNode translate(Codegen g);

	public virtual ConstNode constNode() => null;
}

/////////////////////////////
// Sequence of Expressions //
/////////////////////////////
public class ExprSeqNode:Node
{
	public List<ExprNode> exprs = new List<ExprNode>();

	public void push_back(ExprNode e) => exprs.Add(e);

	public int size() => exprs.Count;

	public void semant(Environ e)
	{
		for(int k = 0; k < exprs.Count; ++k)
		{
			if(exprs[k]!=null) exprs[k] = exprs[k].semant(e);
		}
	}
	public TNode translate(Codegen g, bool userlib)
	{
		TNode t = null, l = null;
		for(int k = 0; k < exprs.Count; ++k)
		{
			TNode q = exprs[k].translate(g);

			if(userlib)
			{
				Type ty = exprs[k].sem_type;
				if(ty.stringType())
				{
					q = call("__bbStrToCStr", q);
				}
				else if(ty.structType()!=null)
				{
					q = new TNode(IR.MEM, q);
				}
				else if(ty == Type.void_type)
				{
					q = new TNode(IR.MEM, add(q, iconst(4)));
				}
			}

			TNode p;
			p = new TNode(IR.ARG, null, null, k * 4);
			p = new TNode(IR.MEM, p, null);
			p = new TNode(IR.MOVE, q, p);
			p = new TNode(IR.SEQ, p, null);
			if(l!=null) l.r = p;
			else t = p;
			l = p;
		}
		return t;
	}
	public void castTo(DeclSeq decls, Environ e, bool userlib)
	{
		if((int)exprs.Count > decls.size()) ex("Too many parameters");
		for(int k = 0; k < decls.size(); ++k)
		{
			Decl d = decls.decls[k];
			if(k < exprs.Count && exprs[k]!=null)
			{
				if(userlib && d.type.structType()!=null)
				{
					if(exprs[k].sem_type.structType()==null)
					{
						if(exprs[k].sem_type.intType())
						{
							exprs[k].sem_type = Type.void_type;
						}
						else
						{
							ex("Illegal type conversion");
						}
					}
					continue;
				}

				exprs[k] = exprs[k].castTo(d.type, e);
			}
			else
			{
				if(d.defType is null) ex("Not enough parameters");
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

	public override ExprNode semant(Environ e)
	{
		if(expr.sem_type is null)
		{
			expr = expr.semant(e);
		}

		if(expr.constNode() is ConstNode c)
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
	public override TNode translate(Codegen g)
	{
		TNode t = expr.translate(g);
		if(expr.sem_type == Type.float_type && sem_type == Type.int_type)
		{
			//float->int
			return new TNode(IR.CAST, t, null);
		}
		if(expr.sem_type == Type.int_type && sem_type == Type.float_type)
		{
			//int->float
			return new TNode(IR.FCAST, t, null);
		}
		if(expr.sem_type == Type.string_type && sem_type == Type.int_type)
		{
			//str->int
			return call("__bbStrToInt", t);
		}
		if(expr.sem_type == Type.int_type && sem_type == Type.string_type)
		{
			//int->str
			return call("__bbStrFromInt", t);
		}
		if(expr.sem_type == Type.string_type && sem_type == Type.float_type)
		{
			//str->float
			return fcall("__bbStrToFloat", t);
		}
		if(expr.sem_type == Type.float_type && sem_type == Type.string_type)
		{
			//float->str
			return call("__bbStrFromFloat", t);
		}
		if(expr.sem_type.structType()!=null && sem_type == Type.string_type)
		{
			//obj->str
			return call("__bbObjToStr", t);
		}
		return t;
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

	public override ExprNode semant(Environ e)
	{
		Type t = e.findType(tag);
		sem_decl = e.findFunc(ident);
		if(sem_decl is null || (sem_decl.kind & DECL.FUNC)==0) ex("Function '" + ident + "' not found");
		FuncType f = sem_decl.type.funcType();
		if(t!=null && f.returnType != t) ex("incorrect function return type");
		exprs.semant(e);
		exprs.castTo(f.@params, e, f.cfunc);
		sem_type = f.returnType;
		return this;
	}
	public override TNode translate(Codegen g)
	{
		FuncType f = sem_decl.type.funcType();

		TNode t;
		TNode l = global("_f" + ident);
		TNode r = exprs.translate(g, f.cfunc);

		if(f.userlib)
		{
			l = new TNode(IR.MEM, l);
			usedfuncs.Add(ident);
		}

		if(sem_type == Type.float_type)
		{
			t = new TNode(IR.FCALL, l, r, exprs.size() * 4);
		}
		else
		{
			t = new TNode(IR.CALL, l, r, exprs.size() * 4);
		}

		if(f.returnType.stringType())
		{
			if(f.cfunc)
			{
				t = call("__bbCStrToStr", t);
			}
		}
		return t;
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

	public override ExprNode semant(Environ e)
	{
		var.semant(e);
		sem_type = var.sem_type;
		ConstType c = sem_type.constType();
		if(c is null) return this;
		ExprNode expr = constValue(c);
		//delete this;
		return expr;
	}
	public override TNode translate(Codegen g)
	{
		return var.load(g);
	}
}

public abstract class ConstNode:ExprNode
{
	public override ExprNode semant(Environ e) => this;
	public override ConstNode constNode() => this;

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
	public override TNode translate(Codegen g)
	{
		return new TNode(IR.CONST, null, null, value);
	}
	public override int intValue() => value;
	public override float floatValue() => value;

	public override string stringValue() => /*itoa*/(value).ToString();
};

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
	public override TNode translate(Codegen g)
	{
		return new TNode(IR.CONST, null, null, BitConverter.SingleToInt32Bits(value));// *(int*)&value
	}
	public override int intValue() => (int)MathF.Round(value);
	public override float floatValue() => value;
	public override string stringValue() => /*ftoa*/(value).ToString();
};

/////////////////////
// String constant //
/////////////////////
public class StringConstNode:ConstNode
{
	public string value;
	public StringConstNode(string s)
	{
		value = s;
		sem_type = Type.string_type;
	}
	public override TNode translate(Codegen g)
	{
		string lab = genLabel();
		g.s_data(value, lab);
		return call("__bbStrConst", global(lab));
	}
	public override int intValue() => /*atoi*/int.Parse(value);
	public override float floatValue() => /*atof*/float.Parse(value);
	public override string stringValue() => value;
};

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

	public override ExprNode semant(Environ e)
	{
		expr = expr.semant(e);
		sem_type = expr.sem_type;
		if(sem_type != Type.int_type && sem_type != Type.float_type) ex("Illegal operator for type");
		if(expr.constNode() is ConstNode c)
		{
			ExprNode e2 = null;
			if(sem_type == Type.int_type)
			{
				switch(op)
				{
					case (Keyword)'+':
						e2 = new IntConstNode(+c.intValue());
						break;
					case (Keyword)'-':
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
					case (Keyword)'+':
						e2 = new FloatConstNode(+c.floatValue());
						break;
					case (Keyword)'-':
						e2 = new FloatConstNode(-c.floatValue());
						break;
					case Keyword.ABS:
						e2 = new FloatConstNode(c.floatValue() >= 0 ? c.floatValue() : -c.floatValue());
						break;
					case Keyword.SGN:
						e2 = new FloatConstNode((float)(c.floatValue() > 0 ? 1 : (c.floatValue() < 0 ? -1 : 0)));
						break;
				}
			}
			//delete this;
			return e2;
		}
		return this;
	}
	public override TNode translate(Codegen g)
	{
		IR n = 0;
		TNode l = expr.translate(g);
		if(sem_type == Type.int_type)
		{
			switch(op)
			{
				case (Keyword)'+': return l;
				case (Keyword)'-':
					n = IR.NEG;
					break;
				case Keyword.ABS: return call("__bbAbs", l);
				case Keyword.SGN: return call("__bbSgn", l);
			}
		}
		else
		{
			switch(op)
			{
				case (Keyword)'+': return l;
				case (Keyword)'-':
					n = IR.FNEG;
					break;
				case Keyword.ABS: return fcall("__bbFAbs", l);
				case Keyword.SGN: return fcall("__bbFSgn", l);
			}
		}
		return new TNode(n, l, null);
	}
};

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

	public override ExprNode semant(Environ e)
	{
		lhs = lhs.semant(e);
		lhs = lhs.castTo(Type.int_type, e);
		rhs = rhs.semant(e);
		rhs = rhs.castTo(Type.int_type, e);
		ConstNode lc = lhs.constNode(), rc = rhs.constNode();
		if(lc!=null && rc!=null)
		{
			ExprNode expr = null;
			switch(op)
			{
				case Keyword.AND:
					expr = new IntConstNode(lc.intValue() & rc.intValue());
					break;
				case Keyword.OR:
					expr = new IntConstNode(lc.intValue() | rc.intValue());
					break;
				case Keyword.XOR:
					expr = new IntConstNode(lc.intValue() ^ rc.intValue());
					break;
				case Keyword.SHL:
					expr = new IntConstNode(lc.intValue() << rc.intValue());
					break;
				case Keyword.SHR:
					expr = new IntConstNode((int)((uint)lc.intValue() >> rc.intValue()));
					break;
				case Keyword.SAR:
					expr = new IntConstNode(lc.intValue() >> rc.intValue());
					break;
			}
			//delete this;
			return expr;
		}
		sem_type = Type.int_type;
		return this;
	}
	public override TNode translate(Codegen g)
	{
		TNode l = lhs.translate(g);
		TNode r = rhs.translate(g);
		IR n = 0;
		switch(op)
		{
			case Keyword.AND:
				n = IR.AND;
				break;
			case Keyword.OR:
				n = IR.OR;
				break;
			case Keyword.XOR:
				n = IR.XOR;
				break;
			case Keyword.SHL:
				n = IR.SHL;
				break;
			case Keyword.SHR:
				n = IR.SHR;
				break;
			case Keyword.SAR:
				n = IR.SAR;
				break;
		}
		return new TNode(n, l, r);
	}
};

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

	public override ExprNode semant(Environ e)
	{
		lhs = lhs.semant(e);
		rhs = rhs.semant(e);
		if(lhs.sem_type.structType()!=null || rhs.sem_type.structType()!=null)
		{
			ex("Arithmetic operator cannot be applied to custom type objects");
		}
		if(lhs.sem_type == Type.string_type || rhs.sem_type == Type.string_type)
		{
			//one side is a string - only + operator...
			if(op != (Keyword)'+') ex("Operator cannot be applied to strings");
			sem_type = Type.string_type;
		}
		else if(op == (Keyword)'^' || lhs.sem_type == Type.float_type || rhs.sem_type == Type.float_type)
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
		ConstNode lc = lhs.constNode(), rc = rhs.constNode();
		if(rc!=null && (op == (Keyword)'/' || op == Keyword.MOD))
		{
			if((sem_type == Type.int_type && rc.intValue()==0) || (sem_type == Type.float_type && rc.floatValue()==0.0))
			{
				ex("Division by zero");
			}
		}
		if(lc!=null && rc!=null)
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
					case (Keyword)'+':
						expr = new IntConstNode(lc.intValue() + rc.intValue());
						break;
					case (Keyword)'-':
						expr = new IntConstNode(lc.intValue() - rc.intValue());
						break;
					case (Keyword)'*':
						expr = new IntConstNode(lc.intValue() * rc.intValue());
						break;
					case (Keyword)'/':
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
					case (Keyword)'+':
						expr = new FloatConstNode(lc.floatValue() + rc.floatValue());
						break;
					case (Keyword)'-':
						expr = new FloatConstNode(lc.floatValue() - rc.floatValue());
						break;
					case (Keyword)'*':
						expr = new FloatConstNode(lc.floatValue() * rc.floatValue());
						break;
					case (Keyword)'/':
						expr = new FloatConstNode(lc.floatValue() / rc.floatValue());
						break;
					case Keyword.MOD:
						expr = new FloatConstNode(lc.floatValue() % rc.floatValue());
						break;
					case (Keyword)'^':
						expr = new FloatConstNode(MathF.Pow(lc.floatValue(), rc.floatValue()));
						break;
				}
			}
			//delete this;
			return expr;
		}
		return this;
	}
	public override TNode translate(Codegen g)
	{
		TNode l = lhs.translate(g);
		TNode r = rhs.translate(g);
		if(sem_type == Type.string_type)
		{
			return call("__bbStrConcat", l, r);
		}
		IR n = 0;
		if(sem_type == Type.int_type)
		{
			switch(op)
			{
				case (Keyword)'+':
					n = IR.ADD;
					break;
				case (Keyword)'-':
					n = IR.SUB;
					break;
				case (Keyword)'*':
					n = IR.MUL;
					break;
				case (Keyword)'/':
					n = IR.DIV;
					break;
				case Keyword.MOD: return call("__bbMod", l, r);
			}
		}
		else
		{
			switch(op)
			{
				case (Keyword)'+':
					n = IR.FADD;
					break;
				case (Keyword)'-':
					n = IR.FSUB;
					break;
				case (Keyword)'*':
					n = IR.FMUL;
					break;
				case (Keyword)'/':
					n = IR.FDIV;
					break;
				case Keyword.MOD: return fcall("__bbFMod", l, r);
				case (Keyword)'^': return fcall("__bbFPow", l, r);
			}
		}
		return new TNode(n, l, r);
	}
};

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

	public override ExprNode semant(Environ e)
	{
		lhs = lhs.semant(e);
		rhs = rhs.semant(e);
		if(lhs.sem_type.structType()!=null || rhs.sem_type.structType()!=null)
		{
			if(op != (Keyword)'=' && op != Keyword.NE) ex("Illegal operator for custom type objects");
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
		ConstNode lc = lhs.constNode(), rc = rhs.constNode();
		if(lc!=null && rc!=null)
		{
			ExprNode expr = null;
			if(opType == Type.string_type)
			{
				switch(op)
				{
					case (Keyword)'<':
						expr = new IntConstNode(lc.stringValue().CompareTo(rc.stringValue())<0 ? 1 : 0);
						break;
					case (Keyword)'=':
						expr = new IntConstNode(lc.stringValue().CompareTo(rc.stringValue()) == 0 ? 1 : 0);
						break;
					case (Keyword)'>':
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
					case (Keyword)'<':
						expr = new IntConstNode(lc.floatValue().CompareTo(rc.floatValue()) < 0 ? 1 : 0);
						break;
					case (Keyword)'=':
						expr = new IntConstNode(lc.floatValue().CompareTo(rc.floatValue()) == 0 ? 1 : 0);
						break;
					case (Keyword)'>':
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
					case (Keyword)'<':
						expr = new IntConstNode(lc.intValue().CompareTo(rc.intValue()) < 0 ? 1 : 0);
						break;
					case (Keyword)'=':
						expr = new IntConstNode(lc.intValue().CompareTo(rc.intValue()) == 0 ? 1 : 0);
						break;
					case (Keyword)'>':
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
	public override TNode translate(Codegen g)
	{
		TNode l = lhs.translate(g);
		TNode r = rhs.translate(g);
		return compare(op, l, r, opType);
	}
};

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
	public override ExprNode semant(Environ e)
	{
		sem_type = e.findType(ident);
		if(sem_type is null) ex("custom type name not found");
		if(sem_type.structType() == null) ex("type is not a custom type");
		return this;
	}
	public override TNode translate(Codegen g)
	{
		return call("__bbObjNew", global("_t" + ident));
	}
};

////////////////////
// First of class //
////////////////////
public class FirstNode:ExprNode
{
	public string ident;
	public FirstNode(string i) { ident = i; }
	public override ExprNode semant(Environ e)
	{
		sem_type = e.findType(ident);
		if(sem_type is null) ex("custom type name name not found");
		return this;
	}
	public override TNode translate(Codegen g)
	{
		return call("__bbObjFirst", global("_t" + ident));
	}
};

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
	public override ExprNode semant(Environ e)
	{
		sem_type = e.findType(ident);
		if(sem_type is null) ex("custom type name not found");
		return this;
	}
	public override TNode translate(Codegen g)
	{
		return call("__bbObjLast", global("_t" + ident));
	}
};

////////////////////
// Next of object //
////////////////////
public class AfterNode:ExprNode
{
	public ExprNode expr;
	public AfterNode(ExprNode e) { expr = e; }

	public override ExprNode semant(Environ e)
	{
		expr = expr.semant(e);
		if(expr.sem_type == Type.null_type) ex("'After' cannot be used on 'Null'");
		if(expr.sem_type.structType() == null) ex("'After' must be used with a custom type object");
		sem_type = expr.sem_type;
		return this;
	}
	public override TNode translate(Codegen g)
	{
		TNode t = expr.translate(g);
		if(g.debug) t = jumpf(t, "__bbNullObjEx");
		return call("__bbObjNext", t);
	}
}

////////////////////
// Prev of object //
////////////////////
public class BeforeNode:ExprNode
{
	public ExprNode expr;
	public BeforeNode(ExprNode e) { expr = e; }

	public override ExprNode semant(Environ e)
	{
		expr = expr.semant(e);
		if(expr.sem_type == Type.null_type) ex("'Before' cannot be used with 'Null'");
		if(expr.sem_type.structType() == null) ex("'Before' must be used with a custom type object");
		sem_type = expr.sem_type;
		return this;
	}
	public override TNode translate(Codegen g)
	{
		TNode t = expr.translate(g);
		if(g.debug) t = jumpf(t, "__bbNullObjEx");
		return call("__bbObjPrev", t);
	}
};

/////////////////
// Null object //
/////////////////
public class NullNode:ExprNode
{
	public override ExprNode semant(Environ e)
	{
		sem_type = Type.null_type;
		return this;
	}
	public override TNode translate(Codegen g)
	{
		return new TNode(IR.CONST, null, null, 0);
	}
};

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

	public override ExprNode semant(Environ e)
	{
		expr = expr.semant(e);
		expr = expr.castTo(Type.int_type, e);
		sem_type = e.findType(type_ident);
		if(sem_type is null) ex("custom type name not found");
		if(sem_type.structType() is null) ex("type is not a custom type");
		return this;
	}
	public override TNode translate(Codegen g)
	{
		TNode t = expr.translate(g);
		t = call("__bbObjFromHandle", t, global("_t" + sem_type.structType().ident));
		return t;
	}
};

///////////////////
// Object Handle //
///////////////////
public class ObjectHandleNode:ExprNode
{
	public ExprNode expr;
	public ObjectHandleNode(ExprNode e){expr = e;}

	public override ExprNode semant(Environ e)
	{
		expr = expr.semant(e);
		if(expr.sem_type.structType() is null) ex("'ObjectHandle' must be used with an object");
		sem_type = Type.int_type;
		return this;
	}
	public override TNode translate(Codegen g)
	{
		TNode t = expr.translate(g);
		return call("__bbObjToHandle", t);
	}
};