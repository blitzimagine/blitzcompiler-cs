using System.Linq;

namespace Blitz3D.Parsing
{
	public abstract class Type
	{
		public abstract string Name{get;}

		//operators
		public virtual bool CanCastTo(Type t) => this == t;

		//built in types
		public readonly static Type void_type = new VoidType();
		public readonly static Type int_type = new IntType();
		public readonly static Type float_type = new FloatType();
		public readonly static Type string_type = new StringType();
		public readonly static Type null_type = new StructType("Null");

		public static Type FromTag(string tag) => tag switch
		{
			"%" => int_type,
			"#" => float_type,
			"$" => string_type,
			"" => int_type,
			_ => int_type,/*throw new System.Exception("Unknown type")*/
		};
	};

	public class FuncType:Type
	{
		public override string Name => "__Func__";

		public readonly Type returnType;
		public readonly DeclSeq @params;
		public readonly bool userlib, cfunc;
		public FuncType(Type t, DeclSeq p, bool ulib, bool cfn)
		{
			returnType = t;
			@params = p;
			userlib = ulib;
			cfunc = cfn;
		}
	};

	public class ArrayType:Type
	{
		public override string Name => $"{elementType.Name}[]";

		public readonly Type elementType;
		public readonly int dims;
		public ArrayType(Type t, int n)
		{
			elementType = t;
			dims = n;
		}
	}

	public class StructType:Type
	{
		public override string Name => ident;

		public readonly string ident;
		public readonly DeclSeq fields;

		public StructType(string i, DeclSeq f = null)
		{
			ident = i;
			fields = f;
		}

		public override bool CanCastTo(Type t) => t == this || t == null_type || (this == null_type && t is StructType);
	};

	public class ConstType:Type
	{
		public override string Name
		{
			get
			{
				if(valueType is IntType)return intValue.ToString();
				if(valueType is FloatType)return floatValue.ToString();
				return stringValue;
			}
		}

		public readonly Type valueType;
		public readonly int intValue;
		public readonly float floatValue;
		public readonly string stringValue;

		public ConstType(int n)
		{
			valueType = int_type;
			intValue = n;
		}
		public ConstType(float n)
		{
			valueType = float_type;
			floatValue = n;
		}
		public ConstType(string n)
		{
			valueType = string_type;
			stringValue = n;
		}
	}

	public class VectorType:Type
	{
		public override string Name => $"List<{elementType.Name}>";

		public readonly string label;
		public readonly Type elementType;
		public readonly int[] sizes;
		public VectorType(string l, Type t, int[] szs)
		{
			label = l;
			elementType = t;
			sizes = szs;
		}

		public override bool CanCastTo(Type t)
		{
			if(this == t){return true;}

			if(!(t is VectorType v)){return false;}
			if(elementType != v.elementType){return false;}
			if(sizes.Length != v.sizes.Length){return false;}
			if(!Enumerable.SequenceEqual(sizes, v.sizes)){return false;}
			
			return true;
		}
	}


	public class VoidType:Type
	{
		public override string Name => "void";

		public override bool CanCastTo(Type t) => t == void_type;
	}

	public class IntType:Type
	{
		public override string Name => "int";

		public override bool CanCastTo(Type t) => t == int_type || t == float_type || t == string_type;
	}

	public class FloatType:Type
	{
		public override string Name => "float";

		public override bool CanCastTo(Type t) => t == int_type || t == float_type || t == string_type;
	}

	public class StringType:Type
	{
		public override string Name => "string";

		public override bool CanCastTo(Type t) => t == int_type || t == float_type || t == string_type;
	}
}