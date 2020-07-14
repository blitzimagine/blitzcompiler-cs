using System.Drawing;

namespace Blitz3D.Parsing
{
	public class Label
	{
		public string name; //name of label
		public Point? def;	//pos of defn
		public Point? @ref;  //goto/restore src

		public Label(string n, Point? d, Point? r)
		{
			name = n;
			def = d;
			@ref = r;
		}
	}
}