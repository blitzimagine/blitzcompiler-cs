//#include "ex.h"
//#include "toker.h"
//#include "environ.h"
//#include "codegen.h"

using System.Collections.Generic;

public class Node
{
	//used user funcs...
	public static HashSet<string> usedfuncs = new HashSet<string>();

	//helper funcs
	///////////////////////////////
	// generic exception thrower //
	///////////////////////////////
	public static void ex() => ex("INTERNAL COMPILER ERROR");
	public static void ex(string e) => throw new Ex(e, -1, "");
	public static void ex(string e, int pos) => throw new Ex(e, pos, "");
	public static void ex(string e, int pos, string f) => throw new Ex(e, pos, f);


	private static int genLabel_cnt;
	////////////////////////////////
	// Generate a fresh ASM label //
	////////////////////////////////
	public static string genLabel()
	{
        return "_" + (++genLabel_cnt & 0x7fffffff).ToString();//itoa
    }

	///////////////////////////////
	// Generate a local variable //
	///////////////////////////////
	public static VarNode genLocal(Environ e, Type ty)
	{
		string t = genLabel();
		Decl d = e.decls.insertDecl(t, ty, DECL.LOCAL);
		return new DeclVarNode(d);
	}

	//////////////////////////////////////////////////////////////
    // compare 2 translated operands - return 1 if true, else 0 //
    //////////////////////////////////////////////////////////////
	public static TNode compare(Keyword op, TNode l, TNode r, Type ty)
    {
        IR n = 0;
        if (ty == Type.float_type)
        {
            switch (op)
            {
            case Keyword.EQ: n = IR.FSETEQ;
                break;
            case Keyword.NE: n = IR.FSETNE;
                break;
            case Keyword.LT: n = IR.FSETLT;
                break;
            case Keyword.GT: n = IR.FSETGT;
                break;
            case Keyword.LE: n = IR.FSETLE;
                break;
            case Keyword.GE: n = IR.FSETGE;
                break;
            }
        }
		else
        {
            switch (op)
            {
            case Keyword.EQ: n = IR.SETEQ;
                break;
            case Keyword.NE: n = IR.SETNE;
                break;
            case Keyword.LT: n = IR.SETLT;
                break;
            case Keyword.GT: n = IR.SETGT;
                break;
            case Keyword.LE: n = IR.SETLE;
                break;
            case Keyword.GE: n = IR.SETGE;
                break;
            }
        }
        if (ty == Type.string_type)
        {
            l = call("__bbStrCompare", l, r);
            r = new TNode(IR.CONST, null, null, 0);
        }
		else if (ty.structType()!=null)
        {
            l = call("__bbObjCompare", l, r);
            r = new TNode(IR.CONST, null, null, 0);
        }
        return new TNode(n, l, r);
    }

	/////////////////////////////////////////////////
	// if type is const, return const value else 0 //
	/////////////////////////////////////////////////
	public static ConstNode constValue(Type ty)
	{
		ConstType c = ty.constType();
		if(c is null) return null;
		ty = c.valueType;
		if(ty == Type.int_type) return new IntConstNode(c.intValue);
		if(ty == Type.float_type) return new FloatConstNode(c.floatValue);
		return new StringConstNode(c.stringValue);
	}

	///////////////////////////////////////////////////////
	// work out var offsets - return size of local frame //
	///////////////////////////////////////////////////////
	public static int enumVars(Environ e)
	{
		//calc offsets
		int p_size = 0, l_size = 0;
		for(int k = 0; k < e.decls.Count; ++k)
		{
			Decl d = e.decls.decls[k];
			if((d.kind & DECL.PARAM)!=0)
			{
				d.offset = p_size + 20;
				p_size += 4;
			}
			else if((d.kind & DECL.LOCAL)!=0)
			{
				d.offset = -4 - l_size;
				l_size += 4;
			}
		}
		return l_size;
	}

    /////////////////////////////////
    // calculate the type of a tag //
    /////////////////////////////////
	public static Type tagType(string tag, Environ e)
    {
        Type t;
        if (tag.Length>0)
        {
            t = e.findType(tag);
            if (t is null) ex("Type \"" + tag + "\" not found");
        } else t = null;
        return t;
    }

	//////////////////////////////
	// initialize all vars to 0 //
	//////////////////////////////
	public static TNode createVars(Environ e)
	{
		int k;
		TNode t = null;
		//initialize locals
		for(k = 0; k < e.decls.Count; ++k)
		{
			Decl d = e.decls.decls[k];
			if(d.kind != DECL.LOCAL) continue;
			if(d.type.vectorType()!=null) continue;
			if(t is null) t = new TNode(IR.CONST, null, null, 0);
			TNode p = new TNode(IR.LOCAL, null, null, d.offset);
			p = new TNode(IR.MEM, p, null);
			t = new TNode(IR.MOVE, t, p);
		}
		//initialize vectors
		for(k = 0; k < e.decls.Count; ++k)
		{
			Decl d = e.decls.decls[k];
			if(d.kind == DECL.PARAM) continue;
			VectorType v = d.type.vectorType();
			if(v is null) continue;
			TNode p = call("__bbVecAlloc", global(v.label));
			TNode m = d.kind == DECL.GLOBAL ? global("_v" + d.name) : local(d.offset);
			p = move(p, mem(m));
			if(t!=null) t = seq(t, p);
			else t = p;
		}
		return t;
	}

	////////////////////////
	// release local vars //
	////////////////////////
	public static TNode deleteVars(Environ e)
	{
		TNode t = null, l = null, p = null, p1 = null, p2 = null;
		for(int k = 0; k < e.decls.Count; ++k)
		{
			Decl d = e.decls.decls[k];
			Type type = d.type;
			string func = string.Empty;
			if(type == Type.string_type)
			{
				if(d.kind == DECL.LOCAL || d.kind == DECL.PARAM)
				{
					func = "__bbStrRelease";
					p1 = mem(local(d.offset));
					p2 = null;
				}
			}
			else if(type.structType()!=null)
			{
				if(d.kind == DECL.LOCAL)
				{
					func = "__bbObjRelease";
					p1 = mem(local(d.offset));
					p2 = null;
				}
			}
			else
			{
				VectorType v = type.vectorType();
				if(v!=null)
				{
					if(d.kind == DECL.LOCAL)
					{
						func = "__bbVecFree";
						p1 = mem(local(d.offset));
						p2 = global(v.label);
					}
				}
			}
			if(func.Length == 0) continue;
			p = new TNode(IR.SEQ, call(func, p1, p2), null);
			(l!=null ? ref l.r : ref t) = p;
			l = p;
		}
		return t;
	}

	public static TNode seq(TNode l, TNode r) => new TNode(IR.SEQ, l, r);
	public static TNode move(TNode src, TNode dest) => new TNode(IR.MOVE, src, dest);
	public static TNode global(string s) => new TNode(IR.GLOBAL, null, null, s);
	public static TNode local(int offset) => new TNode(IR.LOCAL, null, null, offset);
	public static TNode arg(int offset) => new TNode(IR.ARG, null, null, offset);
	public static TNode mem(TNode @ref) => new TNode(IR.MEM, @ref, null);
	public static TNode add(TNode l, TNode r) => new TNode(IR.ADD, l, r);
	public static TNode mul(TNode l, TNode r) => new TNode(IR.MUL, l, r);
	public static TNode iconst(int n) => new TNode(IR.CONST, null, null, n);
	public static TNode ret() => new TNode(IR.RET, null, null);
	public static TNode jsr(string s) => new TNode(IR.JSR, null, null, s);
	public static TNode jump(string s) => new TNode(IR.JUMP, null, null, s);
	public static TNode jumpt(TNode expr, string s) => new TNode(IR.JUMPT, expr, null, s);
	public static TNode jumpf(TNode expr, string s) => new TNode(IR.JUMPF, expr, null, s);
	public static TNode jumpge(TNode l, TNode r, string s) => new TNode(IR.JUMPGE, l, r, s);
	
	//////////////////////////////////////////////////////
    // create a stmt-type function call with int result //
    //////////////////////////////////////////////////////
	public static TNode call(string func, TNode a0 = null, TNode a1 = null, TNode a2 = null)
	{
        int size = 0;
        TNode t = null;
        if (a0!=null)
        {
            t = move(a0, mem(arg(0)));
            size += 4;
            if (a1!=null)
            {
                t = seq(t, move(a1, mem(arg(4))));
                size += 4;
                if (a2!=null)
                {
                    t = seq(t, move(a2, mem(arg(8))));
                    size += 4;
                }
            }
        }
        TNode l = new TNode(IR.GLOBAL, null, null, func);
        return new TNode(IR.CALL, l, t, size);
    }
	
	////////////////////////////////////////////////////////
	// create a stmt-type function call with float result //
	////////////////////////////////////////////////////////
	public static TNode fcall(string func, TNode a0 = null, TNode a1 = null, TNode a2 = null)
	{
		int size = 0;
		TNode t = null;
		if (a0!=null)
		{
			t = move(a0, mem(arg(0)));
			size += 4;
			if (a1!=null)
			{
				t = seq(t, move(a1, mem(arg(4))));
				size += 4;
				if (a2!=null)
				{
					t = seq(t, move(a2, mem(arg(8))));
					size += 4;
				}
			}
		}
		TNode l = new TNode(IR.GLOBAL, null, null, func);
		return new TNode(IR.FCALL, l, t, size);
	}
}