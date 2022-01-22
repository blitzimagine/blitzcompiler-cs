using System.Collections.Generic;
using System.IO;

namespace Blitz3D.Converter.Parsing.Nodes
{
	public class UserFunc
	{
		public string Identifier{get;}
		public string Proc{get;}
		public string Lib{get;}

		public UserFunc(string id, string pr, string lb)
		{
			Identifier = id;
			Proc = pr;
			Lib = lb;
		}
	}

	public class ProgNode:Node
	{
		private readonly DeclSeqNode consts;
		private readonly DeclSeqNode structs;
		private readonly DeclSeqNode funcs;
		private readonly DeclSeqNode datas;
		private readonly StmtSeqNode stmts;

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
		public override void Semant(Environ e)
		{
			Environ env = new Environ(Type.Int, 0, e);

			consts.Proto(env.Decls, env);
			structs.Proto(env.TypeDecls, env);
			structs.Semant(env);
			funcs.Proto(env.FuncDecls, env);
			stmts.Semant(env);
			funcs.Semant(env);
			datas.Proto(env.Decls, env);
			datas.Semant(env);
		}


		public IEnumerable<string> WriteData()
		{
			string thisClass = Path.GetFileNameWithoutExtension(stmts.File).Replace('-','_');
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
			foreach(string s in stmts.WriteData())
			{
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
			globalVars.AddRange(consts.WriteData());

			//data, insert before the }
			lines.InsertRange(lines.Count-1,datas.WriteData());

			HashSet<string> dataVarAdded = new HashSet<string>();
			foreach(var decl in datas)
			{
				DataDeclNode dataDeclNode = (DataDeclNode)decl;
				if(!dataVarAdded.Contains(dataDeclNode.DataVarName))
				{
					dataVarAdded.Add(dataDeclNode.DataVarName);
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

	public class FileNode:Node
	{
		public string FileName{get;}

		//private readonly DeclSeqNode consts;
		//private readonly DeclSeqNode structs;
		//private readonly DeclSeqNode funcs;
		//private readonly DeclSeqNode datas;
		public StmtSeqNode stmts;

		public FileNode(string fileName)
		{
			FileName = Path.GetFileName(fileName);
		}

		public IEnumerable<string> WriteData()
		{
			List<string> globalVars = new List<string>();
			List<string> usingFiles = new List<string>();
			List<string> progStmts = new List<string>();
			foreach(string s in stmts.WriteData())
			{
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
					progStmts.Add(s);
				}
			}

			string thisClass = Path.GetFileNameWithoutExtension(FileName).Replace('-','_');
			List<string> lines = new List<string>();
			lines.AddRange(usingFiles);
			lines.Add($"public static class {thisClass}");
			lines.Add("{");
			lines.AddRange(globalVars);
			lines.Add($"static {thisClass}()");
			lines.AddRange(progStmts);
			lines.Add("}");
			
			return lines;
		}
	}
}