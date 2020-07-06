using System;

public class Ex:Exception
{
	public string ex; //what happened
	public int pos; //source offset
	public string file;
	public Ex(string ex):base(ex)
	{
		this.ex = ex;
		pos = -1;
	}
	public Ex(string ex, int pos, string t)
	{
		this.ex = ex;
		this.pos = pos;
		file = t;
	}
}