namespace Blitz3D.Compiling
{
	public class Label
	{
		public string name; //name of label
		public int def, @ref; //pos of defn and goto/restore src
		public int data_sz; //size of data at this label.

		public Label(string n, int d, int r, int sz)
		{
			name = n;
			def = d;
			@ref = r;
			data_sz = sz;
		}
	}
}