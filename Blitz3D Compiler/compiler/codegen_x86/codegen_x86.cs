//#define NOOPTS

using System.IO;

namespace codegen_86
{
	public class Codegen_x86:Codegen
	{
		public Codegen_x86(TextWriter @out, bool debug) : base(@out, debug)//ostream
		{
			inCode = false;
		}

		public override void enter(string l, int frameSize)
		{
			inCode = true;
			Tile.frameSize = Tile.maxFrameSize = frameSize;
			Tile.codeFrags.Clear();
			Tile.funcLabel = l;
		}
		public override void code(TNode stmt)
		{
			Tile.resetRegs();
			Tile q = munch(stmt);
			q.label();
			q.eval(0);
			q = null;
			stmt = null;
		}

		public override void leave(TNode cleanup, int pop_sz)
		{
			if(cleanup!=null)
			{
				Tile.resetRegs();
				Tile.allocReg(Reg.EAX);
				Tile q = munch(cleanup);
				q.label();
				q.eval(0);
				q = null;
			}
			@out.WriteLine("\t.align\t16");

			if(Tile.funcLabel.Length>0) @out.WriteLine(Tile.funcLabel + ":");

			@out.WriteLine("\tpush\tebx");
			@out.WriteLine("\tpush\tesi");
			@out.WriteLine("\tpush\tedi");
			@out.WriteLine("\tpush\tebp");
			@out.WriteLine("\tmov\tebp,esp");
			if(Tile.maxFrameSize!=0) @out.WriteLine("\tsub\tesp,"+Tile.maxFrameSize);

			int esp_off = 0;
			foreach(string t in Tile.codeFrags)
			{
				if(t.Length>0 && t[0] == '+')
				{
					esp_off += int.Parse(t.Substring(1));
				}
				else if(t.Length>0 && t[0] == '-')
				{
					//***** Still needed for STDCALL *****
					esp_off -= int.Parse(t.Substring(1));
				}
				else
				{
					if(esp_off!=0)
					{
						@out.WriteLine(fixEsp(esp_off));
						esp_off = 0;
					}
					@out.WriteLine(t);
				}
			}
			if(esp_off!=0) @out.WriteLine(fixEsp(esp_off));

			@out.WriteLine("\tmov\tesp,ebp");
			@out.WriteLine("\tpop\tebp");
			@out.WriteLine("\tpop\tedi");
			@out.WriteLine("\tpop\tesi");
			@out.WriteLine("\tpop\tebx");
			@out.WriteLine("\tret\tword " + pop_sz);

			cleanup = null;
			inCode = false;
		}
		public override void label(string l)
		{
			string t = l + ":";
			if(inCode)
				Tile.codeFrags.Add(t);
			else
				Tile.dataFrags.Add(t);
		}
		public override void i_data(int i, string l)
		{
			if(l.Length>0)
			{
				Tile.dataFrags.Add($"{l}:\t.dd\t{i}");
			}
			else
			{
				Tile.dataFrags.Add($"\t.dd\t{i}");
			}
		}
		public override void s_data(string s, string l)
		{
			if(l.Length>0)
			{
				Tile.dataFrags.Add($"{l}:\t.db\t\"{s}\",0");
			}
			else
			{
				Tile.dataFrags.Add($"\t.db\t\"{s}\",0");
			}
		}
		public override void p_data(string p, string l)
		{
			if(l.Length>0)
			{
				Tile.dataFrags.Add($"{l}:\t.dd\t{p}");
			}
			else
			{
				Tile.dataFrags.Add($"\t.dd\t{p}");
			}
		}
		public override void align_data(int n)
		{
			Tile.dataFrags.Add($"\t.align\t{n}");
		}
		public override void flush()
		{
			foreach(string s in Tile.dataFrags)
			{
				@out.WriteLine(s);
			}
			Tile.dataFrags.Clear();
		}

		private bool inCode;

		private Tile genCompare(TNode t, out string func, bool negate)
		{
			switch(t.op)
			{
				case IR.SETEQ: func = negate ? "nz" : "z"; break;
				case IR.SETNE: func = negate ? "z" : "nz"; break;
				case IR.SETLT: func = negate ? "ge" : "l"; break;
				case IR.SETGT: func = negate ? "le" : "g"; break;
				case IR.SETLE: func = negate ? "g" : "le"; break;
				case IR.SETGE: func = negate ? "l" : "ge"; break;
				default: func = null; return null;
			}

			string q;
			TNode ql = null, qr = null;

			if(matchMEM(t.l, out string m1))
			{
				if(matchCONST(t.r, out string c))
				{
					q = "\tcmp\t" + m1 + "," + c;
				}
				else
				{
					q = "\tcmp\t" + m1 + ",%l";
					ql = t.r;
				}
			}
			else
			{
				if(matchMEMCONST(t.r, out string m2))
				{
					q = "\tcmp\t%l," + m2;
					ql = t.l;
				}
				else
				{
					q = "\tcmp\t%l,%r";
					ql = t.l;
					qr = t.r;
				}
			}

			return new Tile(q, ql!=null ? munchReg(ql) : null, qr!=null ? munchReg(qr) : null);
		}

		////////////////////////////////////////////////
		// Integer expressions returned in a register //
		////////////////////////////////////////////////
		private Tile munchUnary(TNode t)
		{
			string s;
			switch(t.op)
			{
				case IR.NEG:
					s = "\tneg\t%l";
					break;
				default: return null;
			}
			return new Tile(s, munchReg(t.l));
		}
		private Tile munchLogical(TNode t)
		{
			string s;
			switch(t.op)
			{
				case IR.AND:
					s = "\tand\t%l,%r";
					break;
				case IR.OR:
					s = "\tor\t%l,%r";
					break;
				case IR.XOR:
					s = "\txor\t%l,%r";
					break;
				default: return null;
			}
			return new Tile(s, munchReg(t.l), munchReg(t.r));
		}
		private Tile munchArith(TNode t)
		{
			if(t.op == IR.DIV)
			{
				if(t.r.op == IR.CONST)
				{
					if(getShift(t.r.iconst, out int shift))
					{
						return new Tile("\tsar\t%l,byte " + shift.ToString(), munchReg(t.l));
					}
				}
				Tile q = new Tile("\tcdq\n\tidiv\tecx", munchReg(t.l), munchReg(t.r));
				q.want_l = Reg.EAX;
				q.want_r = Reg.ECX;
				q.hits = 1 << (int)Reg.EDX;
				return q;
			}

			if(t.op == IR.MUL)
			{
				if(t.r.op == IR.CONST)
				{
					if(getShift(t.r.iconst, out int shift))
					{
						return new Tile("\tshl\t%l,byte " + shift.ToString(), munchReg(t.l));
					}
				}
				else if(t.l.op == IR.CONST)
				{
					if(getShift(t.l.iconst, out int shift))
					{
						return new Tile("\tshl\t%l,byte " + shift.ToString(), munchReg(t.r));
					}
				}
			}

			string op;
			switch(t.op)
			{
				case IR.ADD:
					op = "\tadd\t";
					break;
				case IR.SUB:
					op = "\tsub\t";
					break;
				case IR.MUL:
					op = "\timul\t";
					break;
				default: return null;
			}

			if(matchMEMCONST(t.r, out string s1))
			{
				return new Tile(op + "%l," + s1, munchReg(t.l));
			}
			if(t.op != IR.SUB && matchMEMCONST(t.l, out string s2))
			{
				return new Tile(op + "%l," + s2, munchReg(t.r));
			}
			return new Tile(op + "%l,%r", munchReg(t.l), munchReg(t.r));
		}
		private Tile munchShift(TNode t)
		{
			string op;
			switch(t.op)
			{
				case IR.SHL:
					op = "\tshl\t";
					break;
				case IR.SHR:
					op = "\tshr\t";
					break;
				case IR.SAR:
					op = "\tsar\t";
					break;
				default: return null;
			}

			if(matchCONST(t.r, out string s))
			{
				return new Tile(op + "%l,byte " + s, munchReg(t.l));
			}

			Tile q = new Tile(op + "%l,cl", munchReg(t.l), munchReg(t.r));
			q.want_r = Reg.ECX;
			return q;
		}
		private Tile munchRelop(TNode t)
		{
			Tile q = genCompare(t, out string func, false);

			q = new Tile("\tset" + func + "\tal\n\tmovzx\teax,al", q);
			q.want_l = Reg.EAX;
			return q;
		}

		////////////////////////////////////////////////
		// Float expressions returned on the FP stack //
		////////////////////////////////////////////////
		private Tile munchFPUnary(TNode t)
		{
			string s;
			switch(t.op)
			{
				case IR.FNEG:
					s = "\tfchs";
					break;
				default: return null;
			}
			return new Tile(s, munchFP(t.l));
		}
		private Tile munchFPArith(TNode t)
		{
			string s1, s2 = null;
			switch(t.op)
			{
				case IR.FADD:
					s1 = "\tfaddp\tst(1)";
					break;
				case IR.FMUL:
					s1 = "\tfmulp\tst(1)";
					break;
				case IR.FSUB:
					s1 = "\tfsubrp\tst(1)";
					s2 = "\tfsubp\tst(1)";
					break;
				case IR.FDIV:
					s1 = "\tfdivrp\tst(1)";
					s2 = "\tfdivp\tst(1)";
					break;
				default: return null;
			}
			return new Tile(s1, s2, munchFP(t.l), munchFP(t.r));
		}

		private Tile munchFPRelop(TNode t)
		{
			string s1,s2;
			switch(t.op)
			{
				case IR.FSETEQ:(s1,s2) = ("z","z");break;
				case IR.FSETNE:(s1,s2) = ("nz","nz");break;
				case IR.FSETLT:(s1,s2) = ("b","a");break;
				case IR.FSETGT:(s1,s2) = ("a","b");break;
				case IR.FSETLE:(s1,s2) = ("be","ae");break;
				case IR.FSETGE:(s1,s2) = ("ae","be");break;
				default: return null;
			}
			s1 = "\tfucompp\n\tfnstsw\tax\n\tsahf\n\tset" + s1 + "\tal\n\tmovzx\t%l,al";
			s2 = "\tfucompp\n\tfnstsw\tax\n\tsahf\n\tset" + s2 + "\tal\n\tmovzx\t%l,al";
			Tile q = new Tile(s1, s2, munchFP(t.l), munchFP(t.r));
			q.want_l = Reg.EAX;
			return q;
		}

		///////////////////////////
		// Generic Call handling //
		///////////////////////////
		private Tile munchCall(TNode t)
		{
			Tile q;
			if(t.l.op == IR.GLOBAL)
			{
				q = new Tile("\tcall\t" + t.l.sconst, t.r!=null ? munchReg(t.r) : null);
			}
			else
			{
				q = new Tile("\tcall\t%l", munchReg(t.l), t.r!=null ? munchReg(t.r) : null);
			}
			q.argFrame = t.iconst;
			q.want_l = Reg.EAX;
			q.hits = (1 << (int)Reg.EAX) | (1 << (int)Reg.ECX) | (1 << (int)Reg.EDX);
			return q;
		}

		/////////////////////////////
		// munch and dicard result //
		/////////////////////////////
		private Tile munch(TNode t) //munch and discard result
		{
			if(t is null)
			{
				return null;
			}
			Tile q = null;
			switch(t.op)
			{
				case IR.JSR:
					q = new Tile("\tcall\t" + t.sconst);
					break;
				case IR.RET:
					q = new Tile("\tret");
					break;
				case IR.RETURN:
					q = munchReg(t.l);
					q.want_l = Reg.EAX;
					string s1 = "\tjmp\t" + t.sconst;
					q = new Tile(s1, q);
					break;
				case IR.FRETURN:
					q = munchFP(t.l);
					string s2 = "\tjmp\t" + t.sconst;
					q = new Tile(s2, q);
					break;
				case IR.CALL:
					q = munchCall(t);
					break;
				case IR.JUMP:
					q = new Tile("\tjmp\t" + t.sconst);
					break;
				case IR.JUMPT:
				{
					if(t.l is TNode p)
					{
						bool neg = false;
						if(isRelop(p.op))
						{
							q = genCompare(p, out string func, neg);
							q = new Tile("\tj" + func + "\t" + t.sconst, q);
						}
					}
					break;
				}
				case IR.JUMPF:
				{
					if(t.l is TNode p)
					{
						bool neg = true;
						if(isRelop(p.op))
						{
							q = genCompare(p, out string func, neg);
							q = new Tile("\tj" + func + "\t" + t.sconst, q);
						}
					}
					break;
				}
				case IR.MOVE:
					if(matchMEM(t.r, out string s))
					{
						if(matchCONST(t.l, out string c))
						{
							q = new Tile("\tmov\t" + s + "," + c);
						}
						else if(t.l.op == IR.ADD || t.l.op == IR.SUB)
						{
							TNode p = null;
							if(nodesEqual(t.l.l, t.r)) p = t.l.r;
							else if(t.l.op == IR.ADD && nodesEqual(t.l.r, t.r)) p = t.l.l;
							if(p!=null)
							{
								string op = string.Empty;
								switch(t.l.op)
								{
									case IR.ADD:
										op = "\tadd\t";
										break;
									case IR.SUB:
										op = "\tsub\t";
										break;
								}
								if(matchCONST(p, out string c2))
								{
									q = new Tile(op + s + "," + c2);
								}
								else
								{
									q = new Tile(op + s + ",%l", munchReg(p));
								}
							}
						}
						if(q is null) q = new Tile("\tmov\t" + s + ",%l", munchReg(t.l));
					}
					break;
			}
			if(q is null) q = munchReg(t);
			return q;
		}
		///////////////////////////////////////////
		// munch and return result in a register //
		///////////////////////////////////////////
		private Tile munchReg(TNode t) //munch and put result in a CPU reg
		{
			if(t is null) return null;

			string s;
			Tile q = null;

			switch(t.op)
			{
				case IR.JUMPT:
					q = new Tile("\tand\t%l,%l\n\tjnz\t" + t.sconst, munchReg(t.l));
					break;
				case IR.JUMPF:
					q = new Tile("\tand\t%l,%l\n\tjz\t" + t.sconst, munchReg(t.l));
					break;
				case IR.JUMPGE:
					q = new Tile("\tcmp\t%l,%r\n\tjnc\t" + t.sconst, munchReg(t.l), munchReg(t.r));
					break;
				case IR.CALL:
					q = munchCall(t);
					break;
				case IR.MOVE:
					//MUST BE MOVE TO MEM!
					if(matchMEM(t.r, out s))
					{
						q = new Tile("\tmov\t" + s + ",%l", munchReg(t.l));
					}
					else if(t.r.op == IR.MEM)
					{
						q = new Tile("\tmov\t[%r],%l", munchReg(t.l), munchReg(t.r.l));
					}
					break;
				case IR.MEM:
					if(matchMEM(t, out s))
					{
						q = new Tile("\tmov\t%l," + s);
					}
					else
					{
						q = new Tile("\tmov\t%l,[%l]", munchReg(t.l));
					}
					break;
				case IR.SEQ:
					q = new Tile("", munch(t.l), munch(t.r));
					break;
				case IR.ARG:
					q = new Tile("\tlea\t%l,[esp" + itoa_sgn(t.iconst) + "]");
					break;
				case IR.LOCAL:
					q = new Tile("\tlea\t%l,[ebp" + itoa_sgn(t.iconst) + "]");
					break;
				case IR.GLOBAL:
					q = new Tile("\tmov\t%l," + t.sconst);
					break;
				case IR.CAST:
					q = munchFP(t.l);
					s = "\tpush\t%l\n\tfistp\t[esp]\n\tpop\t%l";
					q = new Tile(s, q);
					break;
				case IR.CONST:
					q = new Tile("\tmov\t%l," + t.iconst.ToString());
					break;
				case IR.NEG:
					q = munchUnary(t);
					break;
				case IR.AND:
				case IR.OR:
				case IR.XOR:
					q = munchLogical(t);
					break;
				case IR.ADD:
				case IR.SUB:
				case IR.MUL:
				case IR.DIV:
					q = munchArith(t);
					break;
				case IR.SHL:
				case IR.SHR:
				case IR.SAR:
					q = munchShift(t);
					break;
				case IR.SETEQ:
				case IR.SETNE:
				case IR.SETLT:
				case IR.SETGT:
				case IR.SETLE:
				case IR.SETGE:
					q = munchRelop(t);
					break;
				case IR.FSETEQ:
				case IR.FSETNE:
				case IR.FSETLT:
				case IR.FSETGT:
				case IR.FSETLE:
				case IR.FSETGE:
					q = munchFPRelop(t);
					break;
				default:
					q = munchFP(t);
					if(q is null) return null;
					s = "\tpush\t%l\n\tfstp\t[esp]\n\tpop\t%l";
					q = new Tile(s, q);
					break;
			}
			return q;
		}

		/////////////////////////////////////////
		// munch and return result on FP stack //
		/////////////////////////////////////////
		private Tile munchFP(TNode t) //munch and put result on FP stack
		{
			if(t is null) return null;

			Tile q;
			switch(t.op)
			{
				case IR.FCALL:
					q = munchCall(t);
					break;
				case IR.FCAST:
					string s1 = "\tpush\t%l\n\tfild\t[esp]\n\tpop\t%l";
					q = new Tile(s1, munchReg(t.l));
					break;
				case IR.FNEG:
					q = munchFPUnary(t);
					break;
				case IR.FADD:
				case IR.FSUB:
				case IR.FMUL:
				case IR.FDIV:
					q = munchFPArith(t);
					break;
				default:
					q = munchReg(t);
					if(q is null) return null;
					string s2 = "\tpush\t%l\n\tfld\t[esp]\n\tpop\t%l";
					q = new Tile(s2, q);
					break;
			}
			return q;
		}

		private static string fixEsp(int esp_off)
		{
			if(esp_off < 0)
			{
				return "\tsub\tesp," + /*itoa*/(-esp_off).ToString();
			}
			return "\tadd\tesp," + /*itoa*/(esp_off).ToString();
		}

		private static string itoa_sgn(int n)
		{
			return n!=0 ? (n > 0 ? "+" + n.ToString() : n.ToString()) : "";
		}

		private static bool isRelop(IR op)
		{
			return op == IR.SETEQ || op == IR.SETNE || op == IR.SETLT || op == IR.SETGT || op == IR.SETLE || op == IR.SETGE;
		}

		private static bool nodesEqual(TNode t1, TNode t2)
		{
			if(t1.op != t2.op ||
				t1.iconst != t2.iconst ||
				t1.sconst != t2.sconst)
				return false;

			if(t1.l!=null)
			{
				if(t2.l is null || !nodesEqual(t1.l, t2.l)) return false;
			}
			else if(t2.l!=null) return false;

			if(t1.r!=null)
			{
				if(t2.r is null || !nodesEqual(t1.r, t2.r)) return false;
			}
			else if(t2.r!=null) return false;

			return true;
		}

		private static bool getShift(int n, out int shift)
		{
#if NOOPTS
	        return false;
#endif

			for(shift = 0; shift < 32; ++shift)
			{
				if((1 << shift) == n) return true;
			}
			return false;
		}

		private static bool matchMEM(TNode t, out string s)
		{
#if NOOPTS
	        return false;
#endif

			if(t.op != IR.MEM)
			{
				s = null;
				return false;
			}
			t = t.l;
			switch(t.op)
			{
				case IR.GLOBAL:
					s = "[" + t.sconst + "]";
					return true;
				case IR.LOCAL:
					s = "[ebp" + itoa_sgn(t.iconst) + "]";
					return true;
				case IR.ARG:
					s = "[esp" + itoa_sgn(t.iconst) + "]";
					return true;
				default:
					s = null;
					return false;
			}
		}

		private static bool matchCONST(TNode t, out string s)
		{
#if NOOPTS
	        return false;
#endif

			switch(t.op)
			{
				case IR.CONST:
					s = t.iconst.ToString();
					return true;
				case IR.GLOBAL:
					s = t.sconst;
					return true;
			}
			s = null;
			return false;
		}

		private static bool matchMEMCONST(TNode t, out string s)
		{
#if NOOPTS
	        return false;
#endif

			return matchMEM(t, out s) || matchCONST(t, out s);
		}
	}
}