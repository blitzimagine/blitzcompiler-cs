using System.Drawing;

namespace Blitz3D.Parsing
{
	public class Label
	{
		public string name; //name of label
		public Point? def;	//pos of defn
		public Point? @ref;  //goto/restore src
		public int data_sz; //size of data at this label.

		public Label(string n, Point? d, Point? r, int sz)
		{
			name = n;
			def = d;
			@ref = r;
			data_sz = sz;
		}
	}
}