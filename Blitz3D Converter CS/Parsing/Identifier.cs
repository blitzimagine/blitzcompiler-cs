namespace Blitz3D.Converter.Parsing
{
	public class Identifier
	{
		public string ID{get;}

		public string Name;

		public Identifier(string id)
		{
			ID = id.ToLowerInvariant();
		}

		public override string ToString() => Name;
	}
}
