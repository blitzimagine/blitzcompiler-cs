using System.Collections.Generic;

public enum DECL
{
	FUNC=1,
	ARRAY=2,
	STRUCT=4,
	//NOT vars
	GLOBAL=8,
	LOCAL=16,
	PARAM=32,
	FIELD=64 //ARE vars
}

public class Decl
{
	public string name;
	public Type type; //type
	public DECL kind;
	public int offset;
	public ConstType defType; //default value
	public Decl(string s, Type t, DECL k, ConstType d = null)
	{
		name = s;
		type = t;
		kind = k;
		defType = d;
	}

	public virtual void getName(ref string buff)
	{
		buff = name;
	}
}

public class DeclSeq
{
	public List<Decl> decls = new List<Decl>();

	public Decl findDecl(string s)
	{
		foreach(Decl decl in decls)
		{
			if(decl.name == s)
			{
				return decl;
			}
		}
		return null;
	}

	public Decl insertDecl(string s, Type t, DECL kind, ConstType d = null)
	{
		if(findDecl(s)!=null)
		{
			return null;
		}
		Decl n = new Decl(s, t, kind, d);
		decls.Add(n);
		return n;
	}
	public int size() => decls.Count;
}