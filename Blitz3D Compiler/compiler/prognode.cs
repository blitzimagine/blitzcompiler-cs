using System.Collections.Generic;

public class UserFunc
{
    public string ident, proc, lib;
    public UserFunc(UserFunc t)
    {
        ident = t.ident;
        proc = t.proc;
        lib = t.lib;
    }
    public UserFunc(string id, string pr, string lb)
    {
        ident = id;
        proc = pr;
        lib = lb;
    }
}

public class ProgNode : Node
{
    public DeclSeqNode consts;
    public DeclSeqNode structs;
    public DeclSeqNode funcs;
    public DeclSeqNode datas;
    public StmtSeqNode stmts;

    public Environ sem_env;

    public string file_lab;

    public ProgNode(DeclSeqNode c, DeclSeqNode s, DeclSeqNode f, DeclSeqNode d, StmtSeqNode ss)
    {
        consts = c;
        structs = s;
        funcs = f;
        datas = d;
        stmts = ss;
    }

    //~ProgNode()
    //{
    //    consts = null;
    //    structs = null;
    //    funcs = null;
    //    datas = null;
    //    stmts = null;
    //}

    //////////////////
    // The program! //
    //////////////////
    public Environ semant(Environ e)
    {
        file_lab = genLabel();

        StmtSeqNode.reset(stmts.file, file_lab);

        Environ env = new Environ(genLabel(), Type.int_type, 0, e);//a_ptr<Environ>

        consts.proto(env.decls, env);
        structs.proto(env.typeDecls, env);
        structs.semant(env);
        funcs.proto(env.funcDecls, env);
        stmts.semant(env);
        funcs.semant(env);
        datas.proto(env.decls, env);
        datas.semant(env);

        sem_env = env/*.release()*/;
        return sem_env;
    }
    public void translate(Codegen g, List<UserFunc> userfuncs)
    {
        int k;

        if (g.debug)
            g.s_data(stmts.file, file_lab);

        //enumerate locals
        int size = enumVars(sem_env);

        //'Main' label
        g.enter("__MAIN", size);

        //reset data pointer
        g.code(call("__bbRestore", global("__DATA")));

        //load external libs
        g.code(call("__bbLoadLibs", global("__LIBS")));

        //call main program
        g.code(jsr(sem_env.funcLabel + "_begin"));
        g.code(jump(sem_env.funcLabel + "_leave"));
        g.label(sem_env.funcLabel + "_begin");

        //create locals
        TNode t = createVars(sem_env);
        if (t!=null)
            g.code(t);
        if (g.debug)
        {
            string t2 = genLabel();
            g.s_data("<main program>", t2);
            g.code(call("__bbDebugEnter", local(0), iconst(sem_env.GetHashCode()), global(t2)));//Hash was originally casting ptr to int
        }

        //no user funcs used!
        usedfuncs.Clear();

        //program statements
        stmts.translate(g);

        //emit return
        g.code(ret());

        //check labels
        for (k = 0; k < (int)sem_env.labels.Count; ++k)
        {
            if (sem_env.labels[k].def < 0)
                ex("Undefined label '" + sem_env.labels[k].name + "'",
                   sem_env.labels[k].@ref, stmts.file);
        }

        //leave main program
        g.label(sem_env.funcLabel + "_leave");
        t = deleteVars(sem_env);
        if (g.debug) t = new TNode(IR.SEQ, call("__bbDebugLeave"), t);
        g.leave(t, 0);

        //structs
        structs.translate(g);

        //non-main functions
        funcs.translate(g);

        //data
        datas.translate(g);

        //library functions
        Dictionary<string, List<int>> libFuncs = new Dictionary<string, List<int>>();

        //lib ptrs
        g.flush();
        g.align_data(4);
        for (k = 0; k < userfuncs.Count; ++k)
        {
            UserFunc fn = userfuncs[k];

            if (!usedfuncs.Contains(fn.ident)) continue;

            libFuncs[fn.lib].Add(k);

            g.i_data(0, "_f" + fn.ident);
        }

        //LIBS chunk
        g.flush();
        g.label("__LIBS");
        foreach(var lf_it in libFuncs)
        {
            //lib name
            g.s_data(lf_it.Key);

            List<int> fns = lf_it.Value;

            for (int j = 0; j < fns.Count; ++j)
            {
                UserFunc fn = userfuncs[fns[j]];

                //proc name
                g.s_data(fn.proc);

                g.p_data("_f" + fn.ident);
            }
            g.s_data("");
        }
        g.s_data("");

        //DATA chunk
        g.flush();
        g.align_data(4);
        g.label("__DATA");
        datas.transdata(g);
        g.i_data(0);

        //Thats IT!
        g.flush();
    }
}