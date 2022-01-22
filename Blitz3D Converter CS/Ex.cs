using System;

namespace Blitz3D.Converter
{
	public class Ex:Exception
	{
		public string File;

		public Ex(string ex, string file=null):base(ex)
		{
			File = file;
		}

		public override string Message
		{
			get
			{
				if(File != null)
				{
					return $"\"{File}\":" + base.Message;
				}
				return base.Message;
			}
		}
	}
}