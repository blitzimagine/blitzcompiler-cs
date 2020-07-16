using System;

namespace Blitz3D.Converter
{
	public class Ex:Exception
	{
		public string file;
		public Ex(string ex):base(ex){}

		public override string Message
		{
			get
			{
				string ret = "";
				if(file != null)
				{
					ret+=$"\"{file}\":";
				}
				return ret + base.Message;
			}
		}

	}
}