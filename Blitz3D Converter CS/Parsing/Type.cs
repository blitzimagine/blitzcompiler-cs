namespace Blitz3D.Converter.Parsing
{
	public abstract class Type
	{
		public abstract string Name{get;}

		public virtual bool IsPrimative{get;}

		//operators
		public virtual bool CanCastTo(Type t) => this == t;

		public virtual bool IsCastImplicit(Type t) => this == t;

		//built-in types
		public static Type Void => VoidType.Instance;
		public static Type Int => IntType.Instance;
		public static Type Float => FloatType.Instance;
		public static Type String => StringType.Instance;
		public static Type Null => NullType.Instance;
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
	public sealed class VectorType:Type
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


	public sealed class VoidType:Type
	{
		public static VoidType Instance{get;} = new VoidType();

		public override string Name => "void";

		public override bool CanCastTo(Type t) => t == Void;

		private VoidType(){}
	}

	public sealed class IntType:Type
	{
		public static IntType Instance{get;} = new IntType();

		public override string Name => "int";
		public override bool IsPrimative => true;

		public override bool CanCastTo(Type t) => t == Int || t == Float || t == String;

		public override bool IsCastImplicit(Type t) => t == Float;

		private IntType(){}
	}

	public sealed class FloatType:Type
	{
		public static FloatType Instance{get;} = new FloatType();

		public override string Name => "float";
		public override bool IsPrimative => true;

		public override bool CanCastTo(Type t) => t == Int || t == Float || t == String;

		private FloatType(){}
	}

	public sealed class StringType:Type
	{
		public static StringType Instance{get;} = new StringType();

		public override string Name => "string";
		public override bool IsPrimative => true;

		public override bool CanCastTo(Type t) => t == Int || t == Float || t == String;

		private StringType(){}
	}

	public sealed class NullType:StructType
	{
		public static NullType Instance{get;} = new NullType();

		private NullType():base("Null"){}

		public override bool CanCastTo(Type t) => t is StructType;
	}
}