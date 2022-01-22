namespace Blitz3D.Converter.Parsing
{
	public class Label:Identifier
	{
		public static Label __DATA{get;} = new Label("__DATA"){Name = "__DATA"};

		public Label(string id):base(id){}
	}
}