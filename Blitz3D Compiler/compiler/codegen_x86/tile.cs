namespace codegen_86
{
	public enum Reg
	{
		BAD_REG = 0,
		eax=1,
		ecx=2,
		edx=3,
		edi=4,
		esi=5,
		ebx=6,
		//reduce to 3 for stress test
		Count = 6
	}

	public class Tile
	{
		public Reg want_l = 0;
		public Reg want_r = 0;
		public int hits = 0;
		public int argFrame = 0;

		private int need = 0;
		private readonly Tile l, r;
		private readonly string assem, assem2;

		public Tile(string a, Tile l = null, Tile r = null)
		{
			this.l = l;
			this.r = r;
			assem = a;
		}
		public Tile(string a, string a2, Tile l = null, Tile r = null)
		{
			this.l = l;
			this.r = r;
			assem = a;
			assem2 = a2;
		}

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
				for(int n = 1; n <= (int)Reg.Count; ++n)
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
				Codegen_x86.codeFrags.Add($"-{argFrame}");
			}

			Reg got_l;
			Reg got_r = 0;
			if(want_l!=0)
			{
				want = want_l;
			}

			string @as = assem;

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
				if(l.need >= (int)Reg.Count && r.need >= (int)Reg.Count)
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
						@as = assem2;
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

			if(@as.Length>0)
			{
				@as = @as.Replace("%l", want_l.ToString());
				@as = @as.Replace("%r", want_r.ToString());
				Codegen_x86.codeFrags.Add(@as);
			}

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
				for(int n = (int)Reg.Count; n >= 1; --n)
				{
					if((spill & (1 << n))!=0)
					{
						popReg((Reg)n);
					}
				}
			}
			return got_l;
		}

		//array of 'used' flags
		private static bool[] regUsed = new bool[(int)Reg.Count + 1];

		//size of locals in function
		internal static int frameSize;
		internal static int maxFrameSize;


		internal static void resetRegs()
		{
			for(int n = 1; n <= (int)Reg.Count; ++n) regUsed[n] = false;
		}

		internal static Reg allocReg(Reg r)
		{
			int n = (int)r;
			if(n==0 || regUsed[n])
			{
				for(n = (int)Reg.Count; n >= 1 && regUsed[n]; --n) { }
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
			if(frameSize > maxFrameSize)
			{
				maxFrameSize = frameSize;
			}
			string s = $"\tmov\t[ebp-{frameSize}],{n}";
			Codegen_x86.codeFrags.Add(s);
		}

		internal static void popReg(Reg n)
		{
			string s = $"\tmov\t{n},[ebp-{frameSize}]";
			Codegen_x86.codeFrags.Add(s);
			frameSize -= 4;
		}

		internal static void moveReg(Reg d, Reg s)
		{
			string t = $"\tmov\t{d},{s}";
			Codegen_x86.codeFrags.Add(t);
		}

		internal static void swapRegs(Reg d, Reg s)
		{
			string t = $"\txchg\t{d},{s}";
			Codegen_x86.codeFrags.Add(t);
		}
	}
}