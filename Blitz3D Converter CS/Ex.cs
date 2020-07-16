using System;
using System.Drawing;

namespace Blitz3D.Converter
{
	public class Ex:Exception
	{
		public Point? pos;//source offset
		public string file;
		public Ex(string ex, Point? pos = null, string file = null):base(ex)
		{
			this.pos = pos;
			this.file = file;
		}

		public override string Message
		{
			get
			{
				string ret = "";
				if(file != null)
				{
					ret+=$"\"{file}\":";
				}
				if(pos is Point posVal)
				{
					int row = posVal.Y + 1;
					int col = posVal.X + 1;
					ret+=$"{row}:{col}:";
				}
				return ret + base.Message;
			}
		}

	}
}