namespace Blitz3D.Converter.Parsing
{
	public abstract class Type
	{
		public abstract string Name{get;}

		//operators
		public virtual bool CanCastTo(Type t) => this == t;

		public virtual bool IsCastImplicit(Type t) => this == t;

		//built-in types
		public static Type Void{get;} = new VoidType();
		public static Type Int{get;} = new IntType();
		public static Type Float{get;} = new FloatType();
		public static Type String{get;} = new StringType();
		public static Type Null{get;} = new NullType();
	}

	public class FuncType:Type
	{
		public override string Name => "__Func__";

		public Type ReturnType{get;}
		public DeclSeq Params{get;}
		public bool CFunc{get;}

		public FuncType(Type t, DeclSeq p, bool cfn)
		{
			ReturnType = t;
			Params = p;
			CFunc = cfn;
		}
	}

	public class ArrayType:Type
	{
		public override string Name => $"{ElementType.Name}[{new string(',', Rank-1)}]";

		public Type ElementType{get;}
		/// <summary>Number of dimensions in an array</summary>
		public int Rank{get;}

		public ArrayType(Type t, int n)
		{
			ElementType = t;
			Rank = n;
		}
	}

	public class StructType:Type
	{
		public override string Name{get;}

		public DeclSeq Fields{get;} = new DeclSeq();

		public StructType(string i)
		{
			Name = i;
		}

		public override bool CanCastTo(Type t) => t == this || t == Null;
	}

	//public class ConstType:Type
	//{
	//	public override string Name
	//	{
	//		get
	//		{
	//			if(valueType is IntType)return intValue.ToString();
	//			if(valueType is FloatType)return floatValue.ToString();
	//			return stringValue;
	//		}
	//	}

	//	public readonly Type valueType;
	//	public readonly int intValue;
	//	public readonly float floatValue;
	//	public readonly string stringValue;

	//	public ConstType(int n)
	//	{
	//		valueType = Int;
	//		intValue = n;
	//	}
	//	public ConstType(float n)
	//	{
	//		valueType = Float;
	//		floatValue = n;
	//	}
	//	public ConstType(string n)
	//	{
	//		valueType = String;
	//		stringValue = n;
	//	}
	//}

	/// <summary>Blitz Array, this is like a C style array.</summary>
	public class VectorType:Type
	{
		public override string Name => $"{ElementType.Name}[{new string(',', Rank-1)}]";

		public Type ElementType{get;}
		///<summary>Number of dimensions</summary>
		public int Rank{get;}
		public VectorType(Type t, int dim)
		{
			ElementType = t;
			Rank = dim;
		}

		public override bool CanCastTo(Type t)
		{
			if(this == t){return true;}

			if(!(t is VectorType v)){return false;}
			if(ElementType != v.ElementType){return false;}
			if(Rank != v.Rank){return false;}
			
			return true;
		}
	}


	public class VoidType:Type
	{
		public override string Name => "void";

		public override bool CanCastTo(Type t) => t == Void;
	}

	public class IntType:Type
	{
		public override string Name => "int";

		public override bool CanCastTo(Type t) => t == Int || t == Float || t == String;

		public override bool IsCastImplicit(Type t) => t == Float;
	}

	public class FloatType:Type
	{
		public override string Name => "float";

		public override bool CanCastTo(Type t) => t == Int || t == Float || t == String;
	}

	public class StringType:Type
	{
		public override string Name => "string";

		public override bool CanCastTo(Type t) => t == Int || t == Float || t == String;
	}

	public class NullType:StructType
	{
		public NullType():base("Null"){}

		public override bool CanCastTo(Type t) => t is StructType;
	}
}