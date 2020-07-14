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
		/// <summary>The resulting type from evaluating the expression</summary>
		public Type sem_type = null;
		public bool NeedsSemant = true;

		//public virtual Type Sem_Type => sem_type;

		public ExprNode CastTo(Type ty, Environ e)
		{
			if(!sem_type.CanCastTo(ty))
			{
				throw new Ex("Illegal type conversion");
			}
			ExprNode expr = this;
			CastNode.CastIfNeeded(ref expr, ty, e);
			return expr;
		}

		public virtual void Semant(Environ e){}

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

		public void Semant(Environ e)
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
					if(userlib && d.type is StructType)
					{
						if(!(exprs[k].sem_type is StructType))
						{
							if(exprs[k].sem_type is IntType)
							{
								exprs[k].sem_type = Type.Void;
							}
							else
							{
								throw new Ex("Illegal type conversion");
							}
						}
						continue;
					}

					exprs[k] = exprs[k].CastTo(d.type, e);
				}
				else
				{
					if(d.defType is null)
					{
						throw new Ex("Not enough parameters");
					}
					ExprNode expr = d.defType;
					if(k < exprs.Count) exprs[k] = expr;
					else exprs.Add(expr);
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

		public override void Semant(Environ e)
		{
			if(expr.NeedsSemant)
			{
				expr.Semant(e);
			}

			sem_type = type;
			NeedsSemant = false;
		}

		public override string JoinedWriteData()
		{
			Type from = expr.sem_type;
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
			return $"({to.Name})({expr.JoinedWriteData()})";
		}

		public static void CastIfNeeded(ref ExprNode expr, Type ty, Environ e)
		{
			if(expr.sem_type != ty)
			{
				expr = new CastNode(expr, ty);
				if(e != null)
				{
					expr.Semant(e);
				}
			}
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
			sem_type = f.returnType;
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

		public override string JoinedWriteData() => $"{ident}({exprs.JoinedWriteData()})";
	}

	/////////////////////////
	// Variable expression //
	/////////////////////////
	public class VarExprNode:ExprNode
	{
		private readonly VarNode var;
		public VarExprNode(VarNode v)
		{
			var = v;
		}

		public override void Semant(Environ e)
		{
			var.Semant(e);
			sem_type = var.sem_type;
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => var.JoinedWriteData();
	}

	public abstract class ConstNode:ExprNode
	{
		private readonly string literal;
		public ConstNode(string literal, Type type)
		{
			this.literal = literal;
			sem_type = type;
		}

		public override string JoinedWriteData() => literal;
	}

	///<summary>Integer constant</summary>
	public class IntConstNode:ConstNode
	{
		public IntConstNode(string literal):base(literal, Type.Int){}
	}

	///<summary>Float constant</summary>
	public class FloatConstNode:ConstNode
	{
		public FloatConstNode(string literal):base(literal+'f', Type.Float){}
	}

	///<summary>String constant</summary>
	public class StringConstNode:ConstNode
	{
		public StringConstNode(string literal):base(literal, Type.String){}
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

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			sem_type = expr.sem_type;
			NeedsSemant = false;
			//if(sem_type != Type.Int && sem_type != Type.Float)
			//{
			//	throw new Ex("Illegal operator for type");
			//}
		}

		public override string JoinedWriteData() => op switch
		{
			Keyword.POSITIVE => $"+{expr.JoinedWriteData()}",
			Keyword.NEGATIVE => $"-{expr.JoinedWriteData()}",
			Keyword.ABS => $"Math.Abs({expr.JoinedWriteData()})",
			Keyword.SGN => $"Math.Sign({expr.JoinedWriteData()})",
			_ => throw new Exception("Invalid operation")
		};
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

		public override void Semant(Environ e)
		{
			lhs.Semant(e);
			lhs = lhs.CastTo(Type.Int, e);
			rhs.Semant(e);
			rhs = rhs.CastTo(Type.Int, e);
			sem_type = Type.Int;
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => op switch
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

		public override void Semant(Environ e)
		{
			lhs.Semant(e);
			rhs.Semant(e);
			if(lhs.sem_type is StructType || rhs.sem_type is StructType)
			{
				throw new Ex("Arithmetic operator cannot be applied to custom type objects");
			}
			if(lhs.sem_type == Type.String || rhs.sem_type == Type.String)
			{
				//one side is a string - only + operator...
				if(op != Keyword.POSITIVE) throw new Ex("Operator cannot be applied to strings");
				sem_type = Type.String;
			}
			else if(op == Keyword.POW || lhs.sem_type == Type.Float || rhs.sem_type == Type.Float)
			{
				//It's ^, or one side is a float
				sem_type = Type.Float;
			}
			else
			{
				//must be 2 ints
				sem_type = Type.Int;
			}
			NeedsSemant = false;
			lhs = lhs.CastTo(sem_type, e);
			rhs = rhs.CastTo(sem_type, e);
		}

		public override string JoinedWriteData() => op switch
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

		public override void Semant(Environ e)
		{
			lhs.Semant(e);
			rhs.Semant(e);
			if(lhs.sem_type is StructType || rhs.sem_type is StructType)
			{
				if(op != Keyword.EQ && op != Keyword.NE) throw new Ex("Illegal operator for custom type objects");
				opType = lhs.sem_type != Type.Null ? lhs.sem_type : rhs.sem_type;
			}
			else if(lhs.sem_type == Type.String || rhs.sem_type == Type.String)
			{
				opType = Type.String;
			}
			else if(lhs.sem_type == Type.Float || rhs.sem_type == Type.Float)
			{
				opType = Type.Float;
			}
			else
			{
				opType = Type.Int;
			}
			sem_type = Type.Int;
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
		public override void Semant(Environ e)
		{
			sem_type = e.findType(ident);
			if(sem_type is null)
			{
				throw new Ex("custom type name not found");
			}
			if(!(sem_type is StructType))
			{
				throw new Ex("type is not a custom type");
			}
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => $"new {ident}()";
	}

	////////////////////
	// First of class //
	////////////////////
	public class FirstNode:ExprNode
	{
		public string ident;
		public FirstNode(string i) { ident = i; }
		public override void Semant(Environ e)
		{
			sem_type = e.findType(ident);
			if(sem_type is null)
			{
				throw new Ex("custom type name name not found");
			}
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => $"__bbObjFirst<{ident}>()";
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
		public override void Semant(Environ e)
		{
			sem_type = e.findType(ident);
			if(sem_type is null)
			{
				throw new Ex("custom type name not found");
			}
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => $"__bbObjLast<{ident}>()";
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

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			if(expr.sem_type == Type.Null)
			{
				throw new Ex("'After' cannot be used on 'Null'");
			}
			if(!(expr.sem_type is StructType))
			{
				throw new Ex("'After' must be used with a custom type object");
			}
			sem_type = expr.sem_type;
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => $"__bbObjNext({expr.JoinedWriteData()})";
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

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			if(expr.sem_type == Type.Null)
			{
				throw new Ex("'Before' cannot be used with 'Null'");
			}
			if(!(expr.sem_type is StructType))
			{
				throw new Ex("'Before' must be used with a custom type object");
			}
			sem_type = expr.sem_type;
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => $"__bbObjPrev({expr.JoinedWriteData()})";
	}

	/////////////////
	// Null object //
	/////////////////
	public class NullNode:ExprNode
	{
		public override void Semant(Environ e)
		{
			sem_type = Type.Null;
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => "null";
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

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			expr = expr.CastTo(Type.Int, e);
			sem_type = e.findType(type_ident);
			if(sem_type is null)
			{
				throw new Ex("custom type name not found");
			}
			if(!(sem_type is StructType))
			{
				throw new Ex("type is not a custom type");
			}
			NeedsSemant = false;
		}
		//public override TNode Translate(Codegen g)
		//{
		//	TNode t = expr.Translate(g);
		//	t = call("__bbObjFromHandle", t, global("_t" + sem_type.structType().ident));
		//	return t;
		//}

		public override string JoinedWriteData() => $"__bbObjFromHandle({expr.JoinedWriteData()}, {type_ident})";
	}

	///////////////////
	// Object Handle //
	///////////////////
	public class ObjectHandleNode:ExprNode
	{
		public ExprNode expr;
		public ObjectHandleNode(ExprNode e) { expr = e; }

		public override void Semant(Environ e)
		{
			expr.Semant(e);
			if(!(expr.sem_type is StructType)) throw new Ex("'ObjectHandle' must be used with an object");
			sem_type = Type.Int;
			NeedsSemant = false;
		}

		public override string JoinedWriteData() => $"__bbObjToHandle({expr.JoinedWriteData()})";
	}
}