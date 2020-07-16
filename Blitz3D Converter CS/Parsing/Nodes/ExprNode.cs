using System;
using System.Collections.Generic;
using System.Linq;

namespace Blitz3D.Converter.Parsing.Nodes
{
	//////////////////////////////////
	// Cast an expression to a type //
	//////////////////////////////////
	public abstract class ExprNode:Node
	{
		public bool NeedsSemant = true;

		/// <summary>The resulting type from evaluating the expression</summary>
		public abstract Type Sem_Type{get;}

		public ExprNode CastTo(Type ty, Environ e)
		{
			if(!Sem_Type.CanCastTo(ty))
			{
				throw new Ex("Illegal type conversion");
			}
			if(!Sem_Type.IsCastImplicit(ty))
			{
				ExprNode expr = new CastNode(this, ty);
				if(e != null)
				{
					expr.Semant(e);
				}
				return expr;
			}
			return this;
		}

		public abstract string JoinedWriteData();
	}

	/////////////////////////////
	// Sequence of Expressions //
	/////////////////////////////
	public class ExprSeqNode:Node
	{
		public readonly List<ExprNode> exprs = new List<ExprNode>();

		public void Add(ExprNode e) => exprs.Add(e);

		public int Count => exprs.Count;

		public override void Semant(Environ e)
		{
			for(int k = 0; k < exprs.Count; ++k)
			{
				exprs[k]?.Semant(e);
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
		public void CastTo(DeclSeq decls, Environ e, bool userlib)
		{
			if(exprs.Count > decls.Count)
			{
				throw new Ex("Too many parameters");
			}
			for(int k = 0; k < decls.Count; ++k)
			{
				Decl d = decls.decls[k];
				if(k < exprs.Count && exprs[k]!=null)
				{
					if(!userlib || !(d.type is StructType))
					{
						exprs[k] = exprs[k].CastTo(d.type, e);
					}
				}
				else if(d.defType is null)
				{
					throw new Ex("Not enough parameters");
				}
			}
		}
		public void CastTo(Type t, Environ e)
		{
			for(int k = 0; k < exprs.Count; ++k)
			{
				exprs[k] = exprs[k].CastTo(t, e);
			}
		}

		public string JoinedWriteData() => string.Join(", ", exprs.Select(e=>e.JoinedWriteData()));
	}

	//////////////////////////////////
	// Cast an expression to a type //
	//////////////////////////////////
	public class CastNode:ExprNode
	{
		public override Type Sem_Type => type;
		public ExprNode expr;
		public Type type;
		public CastNode(ExprNode ex, Type ty)
		{
			expr = ex;
			type = ty;
		}

		public override void Semant(Environ e)
		{
			if(expr.NeedsSemant)
			{
				expr.Semant(e);
			}
			NeedsSemant = false;
		}

		public override string JoinedWriteData()
		{
			Type from = expr.Sem_Type;
			Type to = type;
			if(from == type || from == Type.Null)
			{
				return expr.JoinedWriteData();
			}
			if(to == Type.String)
			{
				//TODO: Either use __bbObjToStr, or auto add ToStrings to custom types.
				return expr.JoinedWriteData() + ".ToString()";
			}
			if(to is StructType)
			{
				throw new NotSupportedException("Can not cast to custom type");
			}
			if(from == Type.String)
			{
				return $"{to.Name}.Parse({expr.JoinedWriteData()})";
			}
			return $"({to.Name}){expr.JoinedWriteData()}";
		}
	}

	///////////////////
	// Function call //
	///////////////////
	public class CallNode:ExprNode
	{
		private readonly string ident;
		private readonly string tag;
		private readonly ExprSeqNode exprs;
		private Decl sem_decl;

		public override Type Sem_Type => ((FuncType)sem_decl.type).returnType;

		public CallNode(string i, string t, ExprSeqNode e)
		{
			ident = i;
			tag = t;
			exprs = e;
		}

		public override void Semant(Environ e)
		{
			Type t = e.findType(tag);
			sem_decl = e.findFunc(ident);
			if(sem_decl is null || (sem_decl.kind & DECL.FUNC)==0)
			{
				throw new Ex($"Function '{ident}' not found");
			}
			FuncType f = (FuncType)sem_decl.type;
			if(t!=null && f.returnType != t)
			{
				throw new Ex("incorrect function return type");
			}
			exprs.Semant(e);
			exprs.CastTo(f.@params, e, f.cfunc);
			NeedsSemant = false;
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

		public override string JoinedWriteData() => $"{sem_decl.Name}({exprs.JoinedWriteData()})";
	}

	/////////////////////////
	// Variable expression //
	/////////////////////////
	public class VarExprNode:ExprNode
	{
		public override Type Sem_Type => var.sem_type;

		private readonly VarNode var;
		public VarExprNode(VarNode v)
		{
			var = v;
		}

		public override void Semant(Environ e)
		{
			var.Semant(e);
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => var.JoinedWriteData();
	}

	public abstract class ConstNode:ExprNode
	{
		private readonly string literal;
		public ConstNode(string literal)
		{
			this.literal = literal;
		}

		public override string JoinedWriteData() => literal;
	}

	///<summary>Integer constant</summary>
	public class IntConstNode:ConstNode
	{
		public override Type Sem_Type => Type.Int;
		public IntConstNode(string literal):base(literal){}
	}

	///<summary>Float constant</summary>
	public class FloatConstNode:ConstNode
	{
		public override Type Sem_Type => Type.Float;
		public FloatConstNode(string literal):base(literal[0]=='.' ? '0'+literal+'f' : literal+'f'){}
	}

	///<summary>String constant</summary>
	public class StringConstNode:ConstNode
	{
		public override Type Sem_Type => Type.String;
		public StringConstNode(string literal):base(literal){}
	}

	////////////////////
	// Unary operator //
	////////////////////
	public class UniExprNode:ExprNode
	{
		public override Type Sem_Type => expr.Sem_Type;
		public TokenType op;
		public ExprNode expr;
		public UniExprNode(TokenType op, ExprNode expr)
		{
			this.op = op;
			this.expr = expr;
		}

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => op switch
		{
			TokenType.POSITIVE => $"+{expr.JoinedWriteData()}",
			TokenType.NEGATIVE => $"-{expr.JoinedWriteData()}",
			TokenType.ABS => $"Math.Abs({expr.JoinedWriteData()})",
			TokenType.SGN => $"Math.Sign({expr.JoinedWriteData()})",
			TokenType.NOT => $"!{expr.JoinedWriteData()}",
			_ => throw new Exception("Invalid operation")
		};
	}

	/////////////////////////////////////////////////////
	// boolean expression - accepts ints, returns ints //
	/////////////////////////////////////////////////////
	// and, or, eor, lsl, lsr, asr
	public class BinExprNode:ExprNode
	{
		public override Type Sem_Type => Type.Int;
		public TokenType op;
		public ExprNode lhs, rhs;
		public BinExprNode(TokenType op, ExprNode lhs, ExprNode rhs)
		{
			this.op = op;
			this.lhs = lhs;
			this.rhs = rhs;
		}

		public override void Semant(Environ e)
		{
			lhs.Semant(e);
			lhs = lhs.CastTo(Type.Int, e);
			rhs.Semant(e);
			rhs = rhs.CastTo(Type.Int, e);
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => op switch
		{
			TokenType.AND => $"({lhs.JoinedWriteData()} & {rhs.JoinedWriteData()})",
			TokenType.OR => $"({lhs.JoinedWriteData()} | {rhs.JoinedWriteData()})",
			TokenType.XOR => $"({lhs.JoinedWriteData()} ^ {rhs.JoinedWriteData()})",
			TokenType.SHL => $"({lhs.JoinedWriteData()} << {rhs.JoinedWriteData()})",
			TokenType.SHR => $"(int)((uint){lhs.JoinedWriteData()} >> {rhs.JoinedWriteData()})",
			TokenType.SAR => $"({lhs.JoinedWriteData()} >> {rhs.JoinedWriteData()})",
			_ => throw new Exception("Invalid operation")
		};
	}

	///////////////////////////
	// arithmetic expression //
	///////////////////////////
	// *,/,Mod,+,-
	public class ArithExprNode:ExprNode
	{
		private Type sem_type = null;
		public override Type Sem_Type => sem_type;

		public TokenType op;
		public ExprNode lhs, rhs;
		public ArithExprNode(TokenType op, ExprNode lhs, ExprNode rhs)
		{
			this.op = op;
			this.lhs = lhs;
			this.rhs = rhs;
		}

		public override void Semant(Environ e)
		{
			lhs.Semant(e);
			rhs.Semant(e);
			if(lhs.Sem_Type == Type.String || rhs.Sem_Type == Type.String)
			{
				//one side is a string - only + operator...
				if(op != TokenType.POSITIVE)
				{
					throw new Ex("Operator cannot be applied to strings");
				}
				sem_type = Type.String;
			}
			else if(op == TokenType.POW || lhs.Sem_Type == Type.Float || rhs.Sem_Type == Type.Float)
			{
				//Either POW, or one operand is a float
				sem_type = Type.Float;
			}
			else
			{
				sem_type = Type.Int;
			}
			NeedsSemant = false;
			lhs = lhs.CastTo(Sem_Type, e);
			rhs = rhs.CastTo(Sem_Type, e);
		}

		public override string JoinedWriteData() => op switch
		{
			TokenType.ADD => $"({lhs.JoinedWriteData()} + {rhs.JoinedWriteData()})",
			TokenType.SUB => $"({lhs.JoinedWriteData()} - {rhs.JoinedWriteData()})",
			TokenType.MUL => $"({lhs.JoinedWriteData()} * {rhs.JoinedWriteData()})",
			TokenType.DIV => $"({lhs.JoinedWriteData()} / {rhs.JoinedWriteData()})",
			TokenType.MOD => $"({lhs.JoinedWriteData()} % {rhs.JoinedWriteData()})",
			TokenType.POW => $"MathF.Pow({lhs.JoinedWriteData()}, {rhs.JoinedWriteData()})",
			_ => throw new Exception("Invalid operation")
		};
	}

	/////////////////////////
	// relation expression //
	/////////////////////////
	//<,=,>,<=,<>,>=
	public class RelExprNode:ExprNode
	{
		public TokenType op;
		public ExprNode lhs, rhs;
		public Type opType;

		public override Type Sem_Type => Type.Int;

		public RelExprNode(TokenType op, ExprNode lhs, ExprNode rhs)
		{
			this.op = op;
			this.lhs = lhs;
			this.rhs = rhs;
		}

		public override void Semant(Environ e)
		{
			lhs.Semant(e);
			rhs.Semant(e);
			if(lhs.Sem_Type is StructType || rhs.Sem_Type is StructType)
			{
				if(op != TokenType.EQ && op != TokenType.NE)
				{
					throw new Ex("Illegal operator for custom type objects");
				}
				opType = lhs.Sem_Type != Type.Null ? lhs.Sem_Type : rhs.Sem_Type;
			}
			else if(lhs.Sem_Type == Type.String || rhs.Sem_Type == Type.String)
			{
				opType = Type.String;
			}
			else if(lhs.Sem_Type == Type.Float || rhs.Sem_Type == Type.Float)
			{
				opType = Type.Float;
			}
			else
			{
				opType = Type.Int;
			}
			NeedsSemant = false;
			lhs = lhs.CastTo(opType, e);
			rhs = rhs.CastTo(opType, e);
		}

		public override string JoinedWriteData()
		{
			if(opType == Type.String)//Compare strings and objects
			{
				//TODO: Make sure this matches __bbStrCompare
				return $"{lhs.JoinedWriteData()}.CompareTo({rhs.JoinedWriteData()})";
			}
			else if(opType is StructType)
			{
				//TODO: Add IComparable to struct by default? This would be __bbObjCompare
				return $"{lhs.JoinedWriteData()}.CompareTo({rhs.JoinedWriteData()})";
			}
			else
			{
				return op switch
				{
					TokenType.LT => $"({lhs.JoinedWriteData()} < {rhs.JoinedWriteData()})",
					TokenType.EQ => $"({lhs.JoinedWriteData()} == {rhs.JoinedWriteData()})",
					TokenType.GT => $"({lhs.JoinedWriteData()} > {rhs.JoinedWriteData()})",
					TokenType.LE => $"({lhs.JoinedWriteData()} <= {rhs.JoinedWriteData()})",
					TokenType.NE => $"({lhs.JoinedWriteData()} != {rhs.JoinedWriteData()})",
					TokenType.GE => $"({lhs.JoinedWriteData()} >= {rhs.JoinedWriteData()})",
					_ => throw new Exception("Invalid operation")
				};
			}
		}
	}

	public abstract class ObjectExprNode:ExprNode
	{
		private Type sem_type = null;
		public override Type Sem_Type => sem_type;

		protected readonly string ident;
		public ObjectExprNode(string i)
		{
			ident = i;
		}

		public override void Semant(Environ e)
		{
			sem_type = e.findType(ident);
			NeedsSemant = false;
		}
	}
	////////////////////
	// New expression //
	////////////////////
	public class NewNode:ObjectExprNode
	{
		public NewNode(string i):base(i){}

		public override string JoinedWriteData() => $"new {Sem_Type.Name}()";
	}

	////////////////////
	// First of class //
	////////////////////
	public class FirstNode:ObjectExprNode
	{
		public FirstNode(string i):base(i){}

		public override string JoinedWriteData() => $"__bbObjFirst<{Sem_Type.Name}>()";
	}

	///////////////////
	// Last of class //
	///////////////////
	public class LastNode:ObjectExprNode
	{
		public LastNode(string i):base(i){}

		public override string JoinedWriteData() => $"__bbObjLast<{Sem_Type.Name}>()";
	};

	////////////////////
	// Next of object //
	////////////////////
	public class AfterNode:ExprNode
	{
		public override Type Sem_Type => expr.Sem_Type;

		private readonly ExprNode expr;
		public AfterNode(ExprNode e)
		{
			expr = e;
		}

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			if(expr.Sem_Type == Type.Null)
			{
				throw new Ex("'After' cannot be used on 'Null'");
			}
			if(!(expr.Sem_Type is StructType))
			{
				throw new Ex("'After' must be used with a custom type object");
			}
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => $"__bbObjNext({expr.JoinedWriteData()})";
	}

	////////////////////
	// Prev of object //
	////////////////////
	public class BeforeNode:ExprNode
	{
		public override Type Sem_Type => expr.Sem_Type;

		public ExprNode expr;
		public BeforeNode(ExprNode e)
		{
			expr = e;
		}

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			if(expr.Sem_Type == Type.Null)
			{
				throw new Ex("'Before' cannot be used with 'Null'");
			}
			if(!(expr.Sem_Type is StructType))
			{
				throw new Ex("'Before' must be used with a custom type object");
			}
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => $"__bbObjPrev({expr.JoinedWriteData()})";
	}

	/////////////////
	// Null object //
	/////////////////
	public class NullNode:ExprNode
	{
		public override Type Sem_Type => Type.Null;
		public override void Semant(Environ e)
		{
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => "null";
	}

	/////////////////
	// Object cast //
	/////////////////
	public class ObjectCastNode:ExprNode
	{
		protected Type sem_type = null;
		public override Type Sem_Type => sem_type;

		public ExprNode expr;
		public string type_ident;
		public ObjectCastNode(ExprNode e, string t)
		{
			expr = e;
			type_ident = t;
		}

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			expr = expr.CastTo(Type.Int, e);
			sem_type = e.findType(type_ident);
			NeedsSemant = false;
		}
		//public override TNode Translate(Codegen g)
		//{
		//	TNode t = expr.Translate(g);
		//	t = call("__bbObjFromHandle", t, global("_t" + sem_type.structType().ident));
		//	return t;
		//}

		public override string JoinedWriteData() => $"__bbObjFromHandle({expr.JoinedWriteData()}, {Sem_Type.Name})";
	}

	///////////////////
	// Object Handle //
	///////////////////
	public class ObjectHandleNode:ExprNode
	{
		public override Type Sem_Type => Type.Int;

		public ExprNode expr;
		public ObjectHandleNode(ExprNode e) { expr = e; }

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			if(!(expr.Sem_Type is StructType))
			{
				throw new Ex("'ObjectHandle' must be used with an object");
			}
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => $"__bbObjToHandle({expr.JoinedWriteData()})";
	}
}