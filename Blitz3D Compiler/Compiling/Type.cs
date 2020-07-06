namespace Blitz3D.Compiling
{
	public class Type
	{
		public virtual bool intType() => false;

		public virtual bool floatType() => false;

		public virtual bool stringType() => false;

		//casts to inherited types
		public virtual FuncType funcType() => null;

		public virtual ArrayType arrayType() => null;

		public virtual StructType structType() => null;

		public virtual ConstType constType() => null;

		public virtual VectorType vectorType() => null;

		public static StructType n = new StructType("Null");

		//operators
		public virtual bool canCastTo(Type t) => this == t;

		//built in types
		public static Type void_type = v_type.v;
		public static Type int_type = i_type.i;
		public static Type float_type = f_type.f;
		public static Type string_type = s_type.s;
		public static Type null_type = n;
	};

	public class FuncType:Type
	{
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

		public override FuncType funcType() => this;
	};

	public class ArrayType:Type
	{
		public readonly Type elementType;
		public readonly int dims;
		public ArrayType(Type t, int n)
		{
			elementType = t;
			dims = n;
		}

		public override ArrayType arrayType()
		{
			return this;
		}
	}

	public class StructType:Type
	{
		public readonly string ident;
		public readonly DeclSeq fields;

		public StructType(string i, DeclSeq f = null)
		{
			ident = i;
			fields = f;
		}

		public override StructType structType() => this;

		public override bool canCastTo(Type t) => t == this || t == null_type || (this == null_type && t.structType()!=null);
	};

	public class ConstType:Type
	{
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

		public override ConstType constType() => this;
	}

	public class VectorType:Type
	{
		public readonly string label;
		public readonly Type elementType;
		public readonly int[] sizes;
		public VectorType(string l, Type t, int[] szs)
		{
			label = l;
			elementType = t;
			sizes = szs;
		}

		public override VectorType vectorType() => this;

		public override bool canCastTo(Type t)
		{
			if(this == t) return true;
			if(t.vectorType() is VectorType v)
			{
				if(elementType != v.elementType) return false;
				if(sizes.Length != v.sizes.Length) return false;
				for(int k = 0; k < sizes.Length; ++k)
				{
					if(sizes[k] != v.sizes[k]) return false;
				}
				return true;
			}
			return false;
		}
	}


	public class v_type:Type
	{
		public static v_type v = new v_type();

		public override bool canCastTo(Type t) => t == void_type;
	}

	public class i_type:Type
	{
		public static i_type i = new i_type();

		public override bool intType() => true;

		public override bool canCastTo(Type t) => t == int_type || t == float_type || t == string_type;
	}

	public class f_type:Type
	{
		public static f_type f = new f_type();

		public override bool floatType() => true;

		public override bool canCastTo(Type t) => t == int_type || t == float_type || t == string_type;
	}

	public class s_type:Type
	{
		public static s_type s = new s_type();

		public override bool stringType() => true;

		public override bool canCastTo(Type t) => t == int_type || t == float_type || t == string_type;
	}
}