using System.Collections.Generic;
using System.IO;
using System.Text;
using Blitz3D.Compiling;

namespace Blitz3D.Parsing
{
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

	public class ProgNode:Node
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

		//////////////////
		// The program! //
		//////////////////
		public Environ Semant(Environ e)
		{
			file_lab = genLabel();

			StmtSeqNode.Reset(stmts.file, file_lab);

			Environ env = new Environ(genLabel(), Type.int_type, 0, e);

			consts.Proto(env.decls, env);
			structs.Proto(env.typeDecls, env);
			structs.Semant(env);
			funcs.Proto(env.funcDecls, env);
			stmts.Semant(env);
			funcs.Semant(env);
			datas.Proto(env.decls, env);
			datas.Semant(env);

			sem_env = env;
			return sem_env;
		}
		public void Translate(Codegen g, List<UserFunc> userfuncs)
		{
			int k;

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
			if(t!=null)
				g.code(t);

			//no user funcs used!
			usedfuncs.Clear();

			//program statements
			stmts.Translate(g);

			//emit return
			g.code(ret());

			//check labels
			for(k = 0; k < sem_env.labels.Count; ++k)
			{
				if(sem_env.labels[k].def < 0)
					ex("Undefined label '" + sem_env.labels[k].name + "'",
					   sem_env.labels[k].@ref, stmts.file);
			}

			//leave main program
			g.label(sem_env.funcLabel + "_leave");
			t = deleteVars(sem_env);
			g.leave(t, 0);

			//structs
			structs.Translate(g);

			//non-main functions
			funcs.Translate(g);

			//data
			datas.Translate(g);

			//library functions
			Dictionary<string, List<int>> libFuncs = new Dictionary<string, List<int>>();

			//lib ptrs
			g.flush();
			g.align_data(4);
			for(k = 0; k < userfuncs.Count; ++k)
			{
				UserFunc fn = userfuncs[k];

				if(!usedfuncs.Contains(fn.ident)) continue;

				if(!libFuncs.TryGetValue(fn.lib, out var value))
				{
					value = new List<int>();
					libFuncs.Add(fn.lib, value);
				}
				value.Add(k);

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

				for(int j = 0; j < fns.Count; ++j)
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


		public override IEnumerable<string> WriteData()
		{
			string thisClass = Path.GetFileNameWithoutExtension(stmts.file);
			List<string> lines = new List<string>();
			
			//int k;

			////enumerate locals
			//int size = enumVars(sem_env);

			////'Main' label
			//g.enter("__MAIN", size);

			////reset data pointer
			//g.code(call("__bbRestore", global("__DATA")));

			////load external libs
			//g.code(call("__bbLoadLibs", global("__LIBS")));

			////call main program
			//g.code(jsr(sem_env.funcLabel + "_begin"));
			//g.code(jump(sem_env.funcLabel + "_leave"));
			//g.label(sem_env.funcLabel + "_begin");

			////create locals
			//TNode t = createVars(sem_env);
			//if(t!=null)
			//	g.code(t);

			////no user funcs used!
			//usedfuncs.Clear();

			
			////emit return
			//g.code(ret());

			////check labels
			//for(k = 0; k < sem_env.labels.Count; ++k)
			//{
			//	if(sem_env.labels[k].def < 0)
			//		ex("Undefined label '" + sem_env.labels[k].name + "'",
			//		   sem_env.labels[k].@ref, stmts.file);
			//}

			////leave main program
			//g.label(sem_env.funcLabel + "_leave");
			//t = deleteVars(sem_env);
			//g.leave(t, 0);

			//structs
			lines.AddRange(structs.WriteData());

			//non-main functions
			lines.AddRange(funcs.WriteData());

			List<string> globalVars = new List<string>();
			List<string> usingFiles = new List<string>();
			////program statements
			lines.Add($"static {thisClass}()");
			lines.Add("{");
			foreach(string s in stmts.WriteData())
			{
				//if(stmt is VarDeclNode varDeclNode && varDeclNode.kind == DECL.GLOBAL)
				//{
					
				//}
				if(s.StartsWith("using static "))
				{
					usingFiles.Add(s);
				}
				else if(s.StartsWith("public static "))
				{
					globalVars.Add(s);
				}
				else
				{
					lines.Add(s);
				}
			}
			//data
			lines.AddRange(datas.WriteData());
			lines.Add("}");

			HashSet<string> dataVarAdded = new HashSet<string>();
			foreach(var decl in datas.decls)
			{
				DataDeclNode dataDeclNode = (DataDeclNode)decl;
				if(!dataVarAdded.Contains(dataDeclNode.dataVarName))
				{
					dataVarAdded.Add(dataDeclNode.dataVarName);
					globalVars.Add(dataDeclNode.WriteData_InstanceDeclaration());
				}
			}

			lines.InsertRange(0, globalVars);

			////library functions
			//Dictionary<string, List<int>> libFuncs = new Dictionary<string, List<int>>();

			////lib ptrs
			//g.flush();
			//g.align_data(4);
			//for(k = 0; k < userfuncs.Count; ++k)
			//{
			//	UserFunc fn = userfuncs[k];

			//	if(!usedfuncs.Contains(fn.ident)) continue;

			//	if(!libFuncs.TryGetValue(fn.lib, out var value))
			//	{
			//		value = new List<int>();
			//		libFuncs.Add(fn.lib, value);
			//	}
			//	value.Add(k);

			//	g.i_data(0, "_f" + fn.ident);
			//}

			////LIBS chunk
			//g.flush();
			//g.label("__LIBS");
			//foreach(var lf_it in libFuncs)
			//{
			//	//lib name
			//	g.s_data(lf_it.Key);

			//	List<int> fns = lf_it.Value;

			//	for(int j = 0; j < fns.Count; ++j)
			//	{
			//		UserFunc fn = userfuncs[fns[j]];

			//		//proc name
			//		g.s_data(fn.proc);

			//		g.p_data("_f" + fn.ident);
			//	}
			//	g.s_data("");
			//}
			//g.s_data("");

			////DATA chunk
			//g.flush();
			//g.align_data(4);
			//g.label("__DATA");
			//datas.transdata(g);
			//g.i_data(0);

			////Thats IT!
			//g.flush();

			lines.InsertRange(0,new[]{$"public static class {thisClass}","{"});
			lines.Add("}");
			
			lines.InsertRange(0, usingFiles);
			return lines;
		}
	}
}