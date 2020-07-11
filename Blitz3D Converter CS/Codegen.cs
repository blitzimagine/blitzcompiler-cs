using System.IO;

namespace Blitz3D.Compiling
{
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
		public readonly IR op; //opcode
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
	}

	public abstract class Codegen
	{
		protected readonly TextWriter @out;

		public Codegen(TextWriter @out)
		{
			this.@out = @out;
		}

		public abstract void enter(string l, int frameSize);
		public abstract void code(TNode code);
		public abstract void leave(TNode cleanup, int pop_sz);
		public abstract void label(string l);
		public abstract void i_data(int i, string l = "");
		public abstract void s_data(string s, string l = "");
		public abstract void p_data(string p, string l = "");
		public abstract void align_data(int n);
		public abstract void flush();
	}
}