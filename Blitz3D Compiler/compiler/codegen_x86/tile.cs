using System.Collections.Generic;

namespace codegen_86
{
	public enum Reg
	{
		EAX=1,
		ECX,
		EDX,
		EDI,
		ESI,
		EBX
	}

	public class Tile
	{
		public Reg want_l, want_r;
		public int hits, argFrame;

		public Tile(string a, Tile l = null, Tile r = null)
		{
			want_l = 0;
			want_r = 0;
			hits = 0;
			argFrame = 0;
			need = 0;
			this.l = l;
			this.r = r;
			assem = a;
		}
		public Tile(string a, string a2, Tile l = null, Tile r = null)
		{
			want_l = 0;
			want_r = 0;
			hits = 0;
			argFrame = 0;
			need = 0;
			this.l = l;
			this.r = r;
			assem = a;
			assem2 = a2;
		}

		//~Tile()
		//{
		//	l = null;
		//	r = null;
		//}

		public void label()
		{
			if(l is null)
			{
				need = 1;
			}
			else if(r is null)
			{
				l.label();
				need = l.need;
			}
			else
			{
				l.label();
				r.label();
				if(l.need == r.need)
				{
					need = l.need + 1;
				}
				else if(l.need > r.need)
				{
					need = l.need;
				}
				else
				{
					need = r.need;
				}
			}
		}
		public Reg eval(Reg want)
		{
			//save any hit registers
			int spill = hits;
			if(want_l!=0) spill |= 1 << (int)want_l;
			if(want_r!=0) spill |= 1 << (int)want_r;
			if(spill!=0)
			{
				for(int n = 1; n <= NUM_REGS; ++n)
				{
					if((spill & (1 << n))!=0)
					{
						if(regUsed[n]) pushReg((Reg)n);
						else spill &= ~(1 << n);
					}
				}
			}

			//if tile needs an argFrame...
			if(argFrame!=0)
			{
				codeFrags.Add("-" + /*itoa*/(argFrame));
			}

			Reg got_l = 0;
			Reg got_r = 0;
			if(want_l!=0)
			{
				want = want_l;
			}

			ref string @as = ref assem;

			if(l is null)
			{
				got_l = allocReg(want);
			}
			else if(r is null)
			{
				got_l = l.eval(want);
			}
			else
			{
				if(l.need >= NUM_REGS && r.need >= NUM_REGS)
				{
					got_r = r.eval(0);
					pushReg(got_r);
					freeReg(got_r);
					got_l = l.eval(want);
					got_r = allocReg(want_r);
					popReg(got_r);
				}
				else if(r.need > l.need)
				{
					got_r = r.eval(want_r);
					got_l = l.eval(want);
				}
				else
				{
					got_l = l.eval(want);
					got_r = r.eval(want_r);
					if(assem2!=null && assem2.Length>0)
					{
						@as = ref assem2;
					}
				}
				if(want_l == got_r || want_r == got_l)
				{
					swapRegs(got_l, got_r);
					Reg t = got_l;
					got_l = got_r;
					got_r = t;
				}
			}

			if(want_l == 0)
			{
				want_l = got_l;
			}
			else if(want_l != got_l) moveReg(want_l, got_l);

			if(want_r==0)
			{
				want_r = got_r;
			}
			else if(want_r != got_r) moveReg(want_r, got_r);

			@as = @as.Replace("%l",regs[(int)want_l]);
			@as = @as.Replace("%r",regs[(int)want_r]);
			//int i;
			//while((i = @as.IndexOf("%l")) != -1)
			//{
			//	@as.replace(i, 2, regs[want_l]);
			//}
			//while((i = @as.IndexOf("%r")) != -1)
			//{
			//	@as.replace(i, 2, regs[want_r]);
			//}

			codeFrags.Add(@as);

			freeReg(got_r);
			if(want_l != got_l) moveReg(got_l, want_l);

			//cleanup argFrame
			if(argFrame!=0)
			{
				//***** Not needed for STDCALL *****
				//		codeFrags.push_back( "+"+itoa(argFrame) );
			}

			//restore spilled regs
			if(spill!=0)
			{
				for(int n = NUM_REGS; n >= 1; --n)
				{
					if((spill & (1 << n))!=0)
					{
						popReg((Reg)n);
					}
				}
			}
			return got_l;
		}

		private int need;
		private Tile l, r;
		private string assem, assem2;

		//reduce to 3 for stress test
		private const int NUM_REGS = 6;

		private static readonly string[] regs = {"???", "eax", "ecx", "edx", "edi", "esi", "ebx"};

		//array of 'used' flags
		private static bool[] regUsed = new bool[NUM_REGS + 1];

		//size of locals in function
		internal static int frameSize, maxFrameSize;

		//code fragments
		internal static List<string> codeFrags = new List<string>();
		internal static List<string> dataFrags = new List<string>();

		//name of function
		internal static string funcLabel;

		internal static void resetRegs()
		{
			for(int n = 1; n <= NUM_REGS; ++n) regUsed[n] = false;
		}

		internal static Reg allocReg(Reg r)
		{
			int n = (int)r;
			if(n==0 || regUsed[n])
			{
				for(n = NUM_REGS; n >= 1 && regUsed[n]; --n) { }
				if(n==0) return 0;
			}
			regUsed[n] = true;
			return (Reg)n;
		}

		internal static void freeReg(Reg n)
		{
			regUsed[(int)n] = false;
		}

		internal static void pushReg(Reg n)
		{
			frameSize += 4;
			if(frameSize > maxFrameSize) maxFrameSize = frameSize;
			//char buff[32];
			//_itoa_s(frameSize, buff, 32, 10);
			string s = "\tmov\t[ebp-";
			s += frameSize.ToString();
			s += "],";
			s += regs[(int)n];
			s += '\n';
			codeFrags.Add(s);
		}

		internal static void popReg(Reg n)
		{
			//char buff[32];
			//_itoa_s(frameSize, buff, 32, 10);
			string s = "\tmov\t";
			s += regs[(int)n];
			s += ",[ebp-";
			s += frameSize.ToString();
			s += "]\n";
			codeFrags.Add(s);
			frameSize -= 4;
		}

		internal static void moveReg(Reg d, Reg s)
		{
			string t = "\tmov\t" + regs[(int)d] + ',' + regs[(int)s] + '\n';
			codeFrags.Add(t);
		}

		internal static void swapRegs(Reg d, Reg s)
		{
			string t = "\txchg\t" + regs[(int)d] + ',' + regs[(int)s] + '\n';
			codeFrags.Add(t);
		}
	}
}