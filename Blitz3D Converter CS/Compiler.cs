using System.IO;

namespace Blitz3D.Compiling
{
	public class Compiler
	{
		

		private readonly FileInfo in_file;
		private readonly FileInfo out_file;

		public Compiler(FileInfo input, FileInfo output)
		{
			in_file = input;
			out_file = output;
		}

		public void Compile()
		{
			
		}
	}
}