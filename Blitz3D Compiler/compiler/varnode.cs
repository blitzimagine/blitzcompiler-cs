public abstract class VarNode : Node
{
    public Type sem_type;

    //get set var
    //////////////////////////////////
    // Common get/set for variables //
    //////////////////////////////////
    public TNode load(Codegen g)
    {
        TNode t = translate(g);
        if (sem_type == Type.string_type) return call("__bbStrLoad", t);
        return mem(t);
    }
    public virtual TNode store(Codegen g, TNode n)
    {
        TNode t = translate(g);
        if (sem_type.structType()!=null) return call("__bbObjStore", t, n);
        if (sem_type == Type.string_type) return call("__bbStrStore", t, n);
        return move(n, mem(t));
    }
    public virtual bool isObjParam() => false;

    //addr of var
    public abstract void semant(Environ e);
    public abstract TNode translate(Codegen g);
}
//#include "decl.h"

    
//////////////////
// Declared var //
//////////////////
public class DeclVarNode : VarNode
{
    public Decl sem_decl;

    public DeclVarNode(Decl d = null)
    {
        sem_decl = d;
        if (d!=null)
        {
            sem_type = d.type;
        }
    }

    public override void semant(Environ e){}

    public override TNode translate(Codegen g)
    {
        if (sem_decl.kind == DECL.GLOBAL)
        {
            return global("_v" + sem_decl.name);
        }
        return local(sem_decl.offset);
    }
    public override TNode store(Codegen g, TNode n)
    {
        if (isObjParam())
        {
            TNode t = translate(g);
            return move(n, mem(t));
        }
        return base.store(g, n);
    }
    public override bool isObjParam() => sem_type.structType()!=null && sem_decl.kind == DECL.PARAM;
}

///////////////
// Ident var //
///////////////
public class IdentVarNode : DeclVarNode
{
    public string ident, tag;
    public IdentVarNode(string i, string t)
    {
        ident = i;
        tag = t;
    }
    public override void semant(Environ e)
    {
        if (sem_decl!=null) return;
        Type t = tagType(tag, e);
        if (t is null) t = Type.int_type;
        if ((sem_decl = e.findDecl(ident))!=null)
        {
            if ((sem_decl.kind & (DECL.GLOBAL | DECL.LOCAL | DECL.PARAM))==0)
            {
                ex("Identifier '" + sem_decl.name + "' may not be used like this");
            }
            Type ty = sem_decl.type;
            if (ty.constType()!=null)
            {
                ty = ty.constType().valueType;
            }
            if (tag.Length>0 && t != ty) ex("Variable type mismatch");
        } else
        {
            //ugly auto decl!
            sem_decl = e.decls.insertDecl(ident, t, DECL.LOCAL);
        }
        sem_type = sem_decl.type;
    }
}

/////////////////
// Indexed Var //
/////////////////
public class ArrayVarNode : VarNode
{
    public string ident, tag;
    public ExprSeqNode exprs;
    public Decl sem_decl;
    public ArrayVarNode(string i, string t, ExprSeqNode e)
    {
        ident = i;
        tag = t;
        exprs = e;
    }

    public override void semant(Environ e)
    {
        exprs.semant(e);
        exprs.castTo(Type.int_type, e);
        Type t = e.findType(tag);
        sem_decl = e.findDecl(ident);
        if (sem_decl is null || (sem_decl.kind & DECL.ARRAY)==0) ex("Array not found");
        ArrayType a = sem_decl.type.arrayType();
        if (t!=null && t != a.elementType) ex("array type mismtach");
        if (a.dims != exprs.size()) ex("incorrect number of dimensions");
        sem_type = a.elementType;
    }
    public override TNode translate(Codegen g)
    {
        TNode t = null;
        for (int k = 0; k < exprs.size(); ++k)
        {
            TNode e = exprs.exprs[k].translate(g);
            if (k!=0)
            {
                TNode s = mem(add(global("_a" + ident), iconst(k * 4 + 8)));
                e = add(t, mul(e, s));
            }
            if (g.debug)
            {
                TNode s = mem(add(global("_a" + ident), iconst(k * 4 + 12)));
                t = jumpge(e, s, "__bbArrayBoundsEx");
            } else t = e;
        }
        t = add(mem(global("_a" + ident)), mul(t, iconst(4)));
        return t;
    }
}

///////////////
// Field var //
///////////////
public class FieldVarNode : VarNode
{
    public ExprNode expr;
    public string ident, tag;
    public Decl sem_field;
    public FieldVarNode(ExprNode e, string i, string t)
    {
        expr = e;
        ident = i;
        tag = t;
    }

    public override void semant(Environ e)
    {
        expr = expr.semant(e);
        StructType s = expr.sem_type.structType();
        if (s is null) ex("Variable must be a Type");
        sem_field = s.fields.findDecl(ident);
        if (sem_field is null) ex("Type field not found");
        sem_type = sem_field.type;
    }
    public override TNode translate(Codegen g)
    {
        TNode t = expr.translate(g);
        if (g.debug) t = jumpf(t, "__bbNullObjEx");
        t = mem(t);
        if (g.debug) t = jumpf(t, "__bbNullObjEx");
        return add(t, iconst(sem_field.offset));
    }
}

////////////////
// Vector var //
////////////////
public class VectorVarNode : VarNode
{
    public ExprNode expr;
    public ExprSeqNode exprs;
    public VectorType vec_type;
    public VectorVarNode(ExprNode e, ExprSeqNode es)
    {
        expr = e;
        exprs = es;
    }

    public override void semant(Environ e)
    {
        expr = expr.semant(e);
        vec_type = expr.sem_type.vectorType();
        if (vec_type is null) ex("Variable must be a Blitz array");
        if (vec_type.sizes.Count != exprs.size()) ex("Incorrect number of subscripts");
        exprs.semant(e);
        exprs.castTo(Type.int_type, e);
        for (int k = 0; k < exprs.size(); ++k)
        {
            ConstNode t = exprs.exprs[k].constNode();
            if (t!=null)
            {
                if (t.intValue() >= vec_type.sizes[k])
                {
                    ex("Blitz array subscript out of range");
                }
            }
        }
        sem_type = vec_type.elementType;
    }
    public override TNode translate(Codegen g)
    {
        int sz = 4;
        TNode t = null;
        for (int k = 0; k < exprs.size(); ++k)
        {
            TNode p;
            ExprNode e = exprs.exprs[k];
            if (e.constNode() is ConstNode t2)
            {
                p = iconst(t2.intValue() * sz);
            }
            else
            {
                p = e.translate(g);
                if (g.debug)
                {
                    p = jumpge(p, iconst(vec_type.sizes[k]), "__bbVecBoundsEx");
                }
                p = mul(p, iconst(sz));
            }
            sz *= vec_type.sizes[k];
            t = t!=null ? add(t, p) : p;
        }
        return add(t, expr.translate(g));
    }
}