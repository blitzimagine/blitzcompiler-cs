using System.Collections.Generic;

public abstract class Module
{
	//public virtual ~Module() {}

	//public abstract int getPC();

	//public abstract void emit(int @byte);
	//public abstract void emitw(int word);
	//public abstract void emitd(int dword);
	//public abstract void emitx(object data, int sz);//void*

	public abstract bool addSymbol(string sym, int pc);
	//public abstract bool addReloc(string dest_sym, int pc, bool pcrel);

	//public abstract bool findSymbol(string sym, ref int pc);
}

public class Linker
{
	public int version() => config.VERSION;
	public Module createModule() => new BBModule();
	public void deleteModule(Module mod) {/*delete mod;*/}

	//extern "C" _declspec(dllexport) Linker* _cdecl linkerGetLinker();

	private static Linker linker = new Linker();
	public static Linker linkerGetLinker() => linker;

	private unsafe class BBModule:Module
	{
		//public BBModule()
		//{
		//	data = null;
		//	data_sz = 0;
		//	pc = 0;
		//}

		//~BBModule()
		//{
		//	Marshal.FreeHGlobal((IntPtr)data);
		//}

		//public override int getPC() => pc;

		//public override void emit(int @byte)
		//{
		//    ensure(1);
		//    data[pc++] = (byte)@byte;
		//}
		//public override void emitw(int word)
		//{
		//    ensure(2);
		//    *(short*)(data + pc) = (short)word;
		//    pc += 2;
		//}
		//public override void emitd(int dword)
		//{
		//    ensure(4);
		//    *(int*)(data + pc) = dword;
		//    pc += 4;
		//}
		//public override void emitx(object mem, int sz)
		//{
		//    ensure(sz);
		//    memcpy(data + pc, mem, sz);
		//    pc += sz;
		//}
		public override bool addSymbol(string sym, int pc)
		{
			if(symbols.ContainsKey(sym)) return false;
			symbols[sym] = pc;
			return true;
		}
		//public override bool addReloc(string dest_sym, int pc, bool pcrel)
		//{
		//	Dictionary<int, string> rel = pcrel ? rel_relocs : abs_relocs;
		//	if(rel.ContainsKey(pc)) return false;
		//	rel[pc] = dest_sym;
		//	return true;
		//}

		//public override bool findSymbol(string sym, ref int pc)
		//{
		//    if(!symbols.TryGetValue(sym, out int value)){return false;}
		//    pc = value + (int)data;
		//    return true;
		//}

		//private byte* data;
		//private int data_sz, pc;

		private Dictionary<string, int> symbols = new Dictionary<string, int>();
		//private Dictionary<int, string> rel_relocs = new Dictionary<int, string>();
		//private Dictionary<int, string> abs_relocs = new Dictionary<int, string>();

		//private bool findSym(string t, Module libs, ref int n)
		//{
		//    if (findSymbol(t, ref n)) return true;
		//    if (libs.findSymbol(t, ref n)) return true;
		//    Console.Error.WriteLine("Blitz Linker Error: Symbol '" + t + "' not found");
		//    return false;
		//}

		//private void ensure(int n)
		//{
		//    if (pc + n <= data_sz) return;
		//    data_sz = data_sz / 2 + data_sz;
		//    if (data_sz < pc + n) data_sz = pc + n;
		//    byte* old_data = data;
		//    data = (byte*)Marshal.AllocHGlobal(data_sz);
		//    Buffer.MemoryCopy(old_data, data, data_sz, pc);
		//    Marshal.FreeHGlobal((IntPtr)old_data);
		//}
	}
}