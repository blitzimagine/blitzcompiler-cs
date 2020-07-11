using System;
using System.Drawing;

namespace Blitz3D
{
	public class Ex:Exception
	{
		public string ex; //what happened
		public Point? pos; //source offset
		public string file;
		public Ex(string ex) : base(ex)
		{
			this.ex = ex;
			pos = null;
		}
		public Ex(string ex, Point? pos, string t)
		{
			this.ex = ex;
			this.pos = pos;
			file = t;
		}
	}
}