//#include "../stdutil/std.h"
//#include "bbruntime.h"
//#include "symbols.h"

//#include "../stdutil/stdutil.h"

using System.Collections.Generic;

public class Debugger{}

public class Runtime
{
    private static List<string> syms = new List<string>();
    private static List<string>.Enumerator sym_it;

    //public Runtime(){}
    //~Runtime(){}
    public int version() => config.VERSION;
    public string nextSym()
    {
        if (syms.Count==0)
        {
            symbols.linkSymbols(rtSym);
            sym_it = syms.GetEnumerator();
        }
        if (!sym_it.MoveNext())
        {
            syms.Clear();
            return null;
        }
        return sym_it.Current;
    }
    //public IEnumerator<string> EnumerateSyms()
    //{
    //    foreach()
    //}

    public virtual int symValue(string sym) => -1;
    public virtual void shutdown()=>syms.Clear();

    private static void rtSym(string sym)
    {
        syms.Add(sym);
    }


    private static Runtime runtime = new Runtime();
    public static Runtime runtimeGetRuntime() => runtime;

}

//extern "C" _declspec(dllexport) Runtime* _cdecl runtimeGetRuntime();

//std::map<std::string, int> global_syms;