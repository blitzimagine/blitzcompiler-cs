//#include "../stdutil/std.h"

using System.IO;

public enum IR
{
    JUMP,
    JUMPT,
    JUMPF,
    JUMPGE,

    SEQ,
    MOVE,
    MEM,
    LOCAL,
    GLOBAL,
    ARG,
    CONST,

    JSR,
    RET,
    AND,
    OR,
    XOR,
    SHL,
    SHR,
    SAR,

    CALL,
    RETURN,
    CAST,
    NEG,
    ADD,
    SUB,
    MUL,
    DIV,
    SETEQ,
    SETNE,
    SETLT,
    SETGT,
    SETLE,
    SETGE,

    FCALL,
    FRETURN,
    FCAST,
    FNEG,
    FADD,
    FSUB,
    FMUL,
    FDIV,
    FSETEQ,
    FSETNE,
    FSETLT,
    FSETGT,
    FSETLE,
    FSETGE,
}

public class TNode
{
    public IR op; //opcode
    public TNode l, r; //args
    public int iconst; //for CONST type_int
    public string sconst; //for CONST type_string

    public TNode(IR op, TNode l = null, TNode r = null)
    {
        this.op = op;
        this.l = l;
        this.r = r;
        iconst = 0;
    }
    public TNode(IR op, TNode l, TNode r, int i)
    {
        this.op = op;
        this.l = l;
        this.r = r;
        iconst = i;
    }
    public TNode(IR op, TNode l, TNode r, string s)
    {
        this.op = op;
        this.l = l;
        this.r = r;
        iconst = 0;
        sconst = s;
    }

    public void log(){}
}

public class Codegen
{
    public TextWriter @out;//ostream
    public bool debug;

    public Codegen(TextWriter @out, bool debug)
    {
        this.@out = @out;
        this.debug = debug;
    }

    public virtual void enter(string l, int frameSize){}
    public virtual void code(TNode code){}
    public virtual void leave(TNode cleanup, int pop_sz){}
    public virtual void label(string l){}
    public virtual void i_data(int i, string l = ""){}
    public virtual void s_data(string s, string l = ""){}
    public virtual void p_data(string p, string l = ""){}
    public virtual void align_data(int n){}
    public virtual void flush(){}
}