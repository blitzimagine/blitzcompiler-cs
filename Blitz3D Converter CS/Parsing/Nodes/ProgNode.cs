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

	public abstract class FileNode:Node
	{
		public string FileName{get;}

		protected DeclSeqNode consts;
		protected DeclSeqNode structs;
		protected DeclSeqNode funcs;
		protected DeclSeqNode datas;
		protected StmtSeqNode stmts;

		//private readonly DeclSeqNode declations;
		//private readonly List<FileNode> includes;

		public FileNode(string fileName)
		{
			FileName = fileName;
		}

		


		public abstract IEnumerable<string> WriteData();
		//{
		//	string thisClass = Path.GetFileNameWithoutExtension(FileName).Replace('-','_');
		//	List<string> lines = new List<string>();

		//	//structs
		//	lines.AddRange(structs.WriteData());

		//	//non-main functions
		//	lines.AddRange(funcs.WriteData());

		//	List<string> globalVars = new List<string>();
		//	List<string> usingFiles = new List<string>();
		//	//program statements
		//	lines.Add($"static {thisClass}()");
		//	foreach(string s in stmts.WriteData())
		//	{
		//		if(s.StartsWith("using static "))
		//		{
		//			usingFiles.Add(s);
		//		}
		//		else if(s.StartsWith("public static "))
		//		{
		//			globalVars.Add(s);
		//		}
		//		else
		//		{
		//			lines.Add(s);
		//		}
		//	}
		//	globalVars.AddRange(consts.WriteData());

		//	//data, insert before the }
		//	lines.InsertRange(lines.Count-1,datas.WriteData());

		//	HashSet<string> dataVarAdded = new HashSet<string>();
		//	foreach(var decl in datas)
		//	{
		//		DataDeclNode dataDeclNode = (DataDeclNode)decl;
		//		if(!dataVarAdded.Contains(dataDeclNode.DataVarName))
		//		{
		//			dataVarAdded.Add(dataDeclNode.DataVarName);
		//			globalVars.Add(dataDeclNode.WriteData_InstanceDeclaration());
		//		}
		//	}

		//	lines.InsertRange(0, globalVars);

		//	////library functions
		//	//Dictionary<string, List<int>> libFuncs = new Dictionary<string, List<int>>();

		//	////lib ptrs
		//	//g.flush();
		//	//g.align_data(4);
		//	//for(k = 0; k < userfuncs.Count; ++k)
		//	//{
		//	//	UserFunc fn = userfuncs[k];

		//	//	if(!usedfuncs.Contains(fn.ident)) continue;

		//	//	if(!libFuncs.TryGetValue(fn.lib, out var value))
		//	//	{
		//	//		value = new List<int>();
		//	//		libFuncs.Add(fn.lib, value);
		//	//	}
		//	//	value.Add(k);

		//	//	g.i_data(0, "_f" + fn.ident);
		//	//}

		//	////LIBS chunk
		//	//g.flush();
		//	//g.label("__LIBS");
		//	//foreach(var lf_it in libFuncs)
		//	//{
		//	//	//lib name
		//	//	g.s_data(lf_it.Key);

		//	//	List<int> fns = lf_it.Value;

		//	//	for(int j = 0; j < fns.Count; ++j)
		//	//	{
		//	//		UserFunc fn = userfuncs[fns[j]];

		//	//		//proc name
		//	//		g.s_data(fn.proc);

		//	//		g.p_data("_f" + fn.ident);
		//	//	}
		//	//	g.s_data("");
		//	//}
		//	//g.s_data("");

		//	////DATA chunk
		//	//g.flush();
		//	//g.align_data(4);
		//	//g.label("__DATA");
		//	//datas.transdata(g);
		//	//g.i_data(0);

		//	////Thats IT!
		//	//g.flush();

		//	lines.InsertRange(0,new[]{$"public static class {thisClass}","{"});
		//	lines.Add("}");
			
		//	lines.InsertRange(0, usingFiles);
		//	return lines;
		//}
	}

	public class ProgNode:FileNode
	{
		public ProgNode(string fileName, DeclSeqNode c, DeclSeqNode s, DeclSeqNode f, DeclSeqNode d, StmtSeqNode ss):base(fileName)
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


		public override IEnumerable<string> WriteData()
		{
			string thisClass = Path.GetFileNameWithoutExtension(FileName).Replace('-','_');
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
			lines.Add($"public static void Main()");
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

	public class FileIncludeNode:FileNode
	{
		public const string IncludeFunctionName = "__RunInclude";
		//private readonly DeclSeqNode consts;
		//private readonly DeclSeqNode structs;
		//private readonly DeclSeqNode funcs;
		//private readonly DeclSeqNode datas;
		//public StmtSeqNode stmts;

		public FileIncludeNode(string fileName):base(Path.GetFileName(fileName)){}

		public void Setup(DeclSeqNode consts, DeclSeqNode structs, DeclSeqNode funcs, DeclSeqNode datas, StmtSeqNode stmts)
		{
			base.consts = consts;
			base.structs = structs;
			base.funcs = funcs;
			base.datas = datas;
			base.stmts = stmts;
		}

		//////////////////
		// The program! //
		//////////////////
		public override void Semant(Environ e)
		{
			Environ env = e;//new Environ(Type.Int, 0, e);

			//consts.Proto(env.Decls, env);
			//structs.Proto(env.TypeDecls, env);
			//structs.Semant(env);
			//funcs.Proto(env.FuncDecls, env);
			stmts.Semant(env);
			//funcs.Semant(env);
			//datas.Proto(env.Decls, env);
			//datas.Semant(env);
		}

		public override IEnumerable<string> WriteData()
		{
			List<string> globalVars = new List<string>();
			List<string> usingFiles = new List<string>();
			List<string> progStmts = new List<string>();

			//structs
			progStmts.AddRange(structs.WriteData());

			//non-main functions
			progStmts.AddRange(funcs.WriteData());

			////program statements
			progStmts.Add($"internal static void {IncludeFunctionName}()");
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
			globalVars.AddRange(consts.WriteData());

			//data, insert before Main's ending }
			progStmts.InsertRange(progStmts.Count-1,datas.WriteData());

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

			string thisClass = Path.GetFileNameWithoutExtension(FileName).Replace('-','_');
			var lines = new List<string>();
			lines.AddRange(usingFiles);
			lines.Add($"public static class {thisClass}");
			lines.Add("{");
			lines.AddRange(globalVars);
			lines.AddRange(progStmts);
			lines.Add("}");
			
			return lines;
		}

		//public override IEnumerable<string> WriteData()
		//{
		//	List<string> globalVars = new List<string>();
		//	List<string> usingFiles = new List<string>();
		//	List<string> progStmts = new List<string>();
		//	foreach(string s in stmts.WriteData())
		//	{
		//		if(s.StartsWith("using static "))
		//		{
		//			usingFiles.Add(s);
		//		}
		//		else if(s.StartsWith("public static "))
		//		{
		//			globalVars.Add(s);
		//		}
		//		else
		//		{
		//			progStmts.Add(s);
		//		}
		//	}

		//	string thisClass = Path.GetFileNameWithoutExtension(FileName).Replace('-','_');
		//	var lines = new List<string>();
		//	lines.AddRange(usingFiles);
		//	lines.Add($"public static class {thisClass}");
		//	lines.Add("{");
		//	lines.AddRange(globalVars);
		//	lines.Add($"internal static void __RunInclude()");
		//	lines.AddRange(progStmts);
		//	lines.Add("}");
			
		//	return lines;
		//}
	}
}