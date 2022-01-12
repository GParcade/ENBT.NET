using System.Collections;
using System.Text;

namespace ENBT.NET
{
	public class EnbtException : Exception
    {
        public EnbtException(string reason) : base(reason) {}
    }

	public class EnbtVarEncode<T> where T : unmanaged, IComparable, IComparable<T>, IConvertible, IEquatable<T>, IFormattable, ISpanFormattable
	{
		private static byte GetByte(BitArray array)
		{
			byte byt = 0;
			for (int i = 7; i >= 0; i--)
				byt = (byte)((byt << 1) | (array[i] ? 1 : 0));
			return byt;
		}
		public static unsafe void FromVar(byte[] ch, out T res) 
		{
			if (Array.Empty<byte>() == ch || ch == null)
			{
				res = default;
				return;
			}
			fixed (T* p = &res)
			{
				byte* bc = (byte*)p;
				BitArray bitArray = new(ch);
				int max_offset = (sizeof(T) / 5 * 5 + (sizeof(T) % 5) > 0 ? 1 : 0) * 8;
				byte currentByte;
				int ibs = 0;
				do
				{
					int bi = 0;
					int ac_ba_pos = ibs << 3;
					currentByte = 0;
					while (bi < 7)
					{
						if (bitArray.Length <= ac_ba_pos)
							break;
						if (ac_ba_pos == max_offset) throw new EnbtException("VarInt is too big");
						currentByte |= (byte)(bitArray[ac_ba_pos++] ? 1 : 0 << bi) ;
						++bi;
					}
					++ibs;
					*bc++ = currentByte;
					currentByte |= (byte)(bitArray[ac_ba_pos] ? 1 : 0 << bi);
				} while ((currentByte & 0b10000000) != 0);
			}
		}
		public static unsafe void ToVar(in T val, out byte[] ch)
		{
			fixed (T* p = &val)
			{
				byte* bc = (byte*)p;
				byte[] va = new byte[sizeof(T)];
				for (int y = 0; y < sizeof(T); y++)
					va[y] = bc[y];
				BitArray bits = new(va);
				

				byte[] res = new byte[(sizeof(T) / 5 * 5 + (sizeof(T) % 5) > 0 ? 1 : 0)];
				int i = 0;
				do
				{
					byte currentByte = (byte)(GetByte(bits) & 0b01111111);
					bits.RightShift(7);
					if (bits.Cast<bool>().Contains(true)) currentByte |= 0b10000000;

					res[i++] = currentByte;
				} while (bits.Cast<bool>().Contains(true));
				if (res.Any(a => a != 0))
					ch = Array.Empty<byte>();
				else
					ch = res;
			}
		}
	}
	public class Enbt : IEnumerable<Enbt>, ICloneable
	{
		static public readonly Enbt Empty = new();
		static public readonly Endian CurEndian = BitConverter.IsLittleEndian ? Endian.little : Endian.big;
		public struct UnkeyedEnumerator : IEnumerator<Enbt>
        {
			private Dictionary<string, Enbt>.Enumerator enumerator;
			private Dictionary<string, Enbt> dic;
			public UnkeyedEnumerator(Dictionary<string, Enbt> dic)
            {
				enumerator = dic.GetEnumerator();
				this.dic = dic;
			}
			public Enbt Current => enumerator.Current.Value;
			object IEnumerator.Current => enumerator.Current.Value;

			public void Dispose() => enumerator.Dispose();

            public bool MoveNext() => enumerator.MoveNext();

			public void Reset() => enumerator = dic.GetEnumerator();
		}

		private object? obj;
		private TypeId _type;
		private Enbt(TypeId type, object? nobj)
		{
			_type = type;
            switch (type.Type)
            {
				case Type.none:
					break;
				case Type.integer:
                    switch (type.Length)
					{
						case LenType.Tiny:
							if (type.IsSigned)
								obj = (sbyte)(nobj ?? 0);
							else
								obj = (byte)(nobj ?? 0);
							break;
						case LenType.Short:
							if (type.IsSigned)
								obj = (short)(nobj ?? 0);
							else
								obj = (ushort)(nobj ?? 0);
							break;
						case LenType.Default:
							if (type.IsSigned)
								obj = (int)(nobj ?? 0);
							else
								obj = (uint)(nobj ?? 0);
							break;
						case LenType.Long:
							if(type.IsSigned)
								obj = (long)(nobj ?? 0);
							else
								obj = (ulong)(nobj ?? 0);
							break;
					}
					break;
				case Type.floating:
					switch (type.Length)
					{
						case LenType.Tiny:
						case LenType.Short:
							throw new EnbtException("Invalid type: floatng can be only default and long size");
						case LenType.Default:
							obj = (float)(nobj ?? 0);
							break;
						case LenType.Long:
							obj = (double)(nobj ?? 0);
							break;
					}
					break;
				case Type.var_integer:
					switch (type.Length)
					{
						case LenType.Tiny:
						case LenType.Short:
							throw new NotImplementedException();
						case LenType.Default:
						case LenType.Long:
							if (type.IsSigned)
								obj = (long)(nobj ?? 0);
							else
								throw new EnbtException("Invalid type: unsigned var_integer");
							break;
					}
					break;
				case Type.uuid:
					obj = (Guid)(nobj ?? Guid.NewGuid());
					break;
				case Type.sarray:
					obj = type.IsSigned ?
						type.Length switch
						{
							LenType.Tiny => (sbyte[])(nobj ?? Array.Empty<sbyte>()),
							LenType.Short => (string)(nobj ?? string.Empty),
							LenType.Default => (int[])(nobj ?? Array.Empty<int>()),
							LenType.Long => (long[])(nobj ?? Array.Empty<long>()),
							_ => null
						} :
						type.Length switch
						{
							LenType.Tiny => (byte[])(nobj ?? Array.Empty<sbyte>()),
							LenType.Short => (string)(nobj ?? string.Empty),
							LenType.Default => (uint[])(nobj ?? Array.Empty<uint>()),
							LenType.Long => (ulong[])(nobj ?? Array.Empty<ulong>()),
							_ => null
						};
					break;
				case Type.compound:
					obj = (Dictionary<string, Enbt>)(nobj ?? new Dictionary<string, Enbt>());
					break;
				case Type.darray:
				case Type.array:
				case Type.structure:
					obj = (List<Enbt>)(nobj ?? new List<Enbt>());
					break;
				case Type.optional:
					if (type.IsSigned) {
						Enbt? tmp = (Enbt?)nobj;
						if (tmp == null)
							break;
						obj = new Enbt(tmp._type, tmp.obj);

					}
					break;
				case Type.bit:
					break;
				default:
					throw new NotImplementedException();
			}
		}


		public struct Version
		{
			public byte Full;
			public byte Major
			{
				get
				{
					return (byte)(Full & 0xF);
				}
				set
				{
					Full &= 0xF0;
					Full |= (byte)(value & 0xF);
				}
			}
			public byte Minor
			{
				get
				{
					return (byte)((Full & 0xF0) >> 4);
				}
				set
				{
					Full &= 0xF;
					Full |= (byte)((value & 0xF0) << 4);
				}
			}
		};
		public enum Endian
		{
			little, big
		};
		public enum LenType
		{
			Tiny,
			Short,
			Default,
			Long
		};
		public enum Type
		{
			none,    //[0byte]
			integer,
			floating,
			var_integer,//ony default and long length
			uuid,   //[16byte]
			sarray, // [(len)]{chars} if signed, endian convert will be enabled(simple array)

			compound,
			//if compound is signed it will be use strings refered by name id
			//len in signed mode can be only tiny and short mode
			//[len][... items]   ((named items list))
			//					item {name id 2byte} {type_id 1byte} (value_define_and_data)
			// else will be used default string but 'string len' encoded as big endian which last 2 bits used as define 'string len' bytes len, ex[00XXXXXX], [01XXXXXX XXXXXXXX], etc... 00 - 1 byte,01 - 2 byte,02 - 4 byte,03 - 8 byte
			//[len][... items]   ((named items list))
			//					item [(len)][chars] {type_id 1byte} (value_define_and_data)

			darray,//		 [len][... items]   ((unnamed items list))
				   //				item {type_id 1byte} (value_define_and_data)


			array,      //[len][type_id 1byte]{... items} /unnamed items array, if contain static value size reader can get one element without readding all elems[int,double,e.t.c..]
						//				item {value_define_and_data}
			structure, //[total types] [type_id ...] {type defies}
			optional,// 'any value'     (contain value if is_signed == true)  /example [optional,unsigned]
					 //	 				 											  /example [optional,signed][utf8_str,tiny][3]{"Yay"}
			bit,//[0byte] bit in is_signed flag from Type_id byte
				//TO-DO
				//vector,		 //[len][type_id 1byte][items define]{... items} /example [vector,tiny][6][sarray,tiny][3]{"WoW"}{"YaY"}{"it\0"}{"is\0"}{"god"}{"!\0\0"}
				//					item {value_data}
				// 
				// 
				//
			__RESERVED_TO_IMPLEMENT_VECTOR__,
			unused1,
			domain
		};
		public struct TypeId
		{
			static public readonly TypeId Empty = new() { Full = 0 };
			public byte Full;
			public bool IsSigned
			{
				get => (Full & 1) == 1;
				set
				{
					Full &= 0xFE;
					Full |= (byte)(value ? 1 : 0);
				}
			}
			public Endian Endian
			{
				get => (Endian)((Full & 2) >> 1);
				set
				{
					Full &= 0xFD;
					Full |= (byte)(((byte)value) << 1);
				}
			}
			public LenType Length
			{
				get => (LenType)((Full & 12) >> 2);
				set
				{
					Full &= 0xF3;
					Full |= (byte)(((byte)value) << 2);
				}
			}
			public Type Type
			{
				get => (Type)((Full & 0xF0) >> 4);
				set
				{
					Full &= 0xF;
					Full |= (byte)(((byte)value) << 4);
				}
			}
		}


		public Enbt()
        {
			_type = TypeId.Empty;
		}
		public Enbt(TypeId type)
        {
			_type = type;
            switch (type.Type)
			{
				case Type.none:
					_type = TypeId.Empty;
					break;
				case Type.integer:
					obj = type.IsSigned ?
						type.Length switch
						{
							LenType.Tiny => (sbyte)0,
							LenType.Short => (short)0,
							LenType.Default => 0,
							LenType.Long => (long)0,
							_ => null
						}: 
						type.Length switch
						{
							LenType.Tiny => (byte)0,
							LenType.Short => (ushort)0,
							LenType.Default => (uint)0,
							LenType.Long => (ulong)0,
							_ => null
						};
					break;
				case Type.floating:
					if (!type.IsSigned)
					{
						throw new EnbtException("Invalid type: floating can be only signed");
					}
					obj = type.Length switch
						{
							LenType.Short => (Half)0,
							LenType.Default => (float)0,
							LenType.Long => (double)0,
							_ => throw new EnbtException("Invalid type: signed floating " + type.Length)
						};
					break;
				case Type.var_integer:
					if (type.IsSigned)
					{
                        obj = type.Length switch
                        {
                            LenType.Default or LenType.Long => (long)0,
                            _ => throw new EnbtException("Invalid type: signed var_integer " + type.Length),
                        };
                    }
                    else
						throw new EnbtException("Invalid type: unsigned var_integer " + type.Length);
					break;
				case Type.uuid:
					obj = new Guid();
					break;
				case Type.sarray:
					obj = type.IsSigned ?
						type.Length switch
						{
							LenType.Tiny => Array.Empty<sbyte>(),
							LenType.Short => string.Empty,
							LenType.Default => Array.Empty<int>(),
							LenType.Long => Array.Empty<long>(),
							_ => null
						} :
						type.Length switch
						{
							LenType.Tiny => Array.Empty<byte>(),
							LenType.Short => string.Empty,
							LenType.Default => Array.Empty<uint>(),
							LenType.Long => Array.Empty<ulong>(),
							_ => null
						};
					break;
				case Type.darray:
				case Type.array:
				case Type.structure:
					obj = new List<Enbt>();
					break;
				case Type.compound:
					obj = new Dictionary<string,Enbt>();
					break;
				case Type.optional:
					obj = null;
					type.IsSigned = false;
					break;
				case Type.bit:
					break;
				default:
					throw new NotImplementedException();
            }
		}
		public Enbt(Type type, int size)
		{
            switch (type)
			{
				case Type.array:
				case Type.darray:
					break;
				default:
					throw new ArgumentException("type will be array Type");
			}
			List<Enbt> enbts = new(size);
			for(int i = 0; i < size; i++)
				enbts.Add(new Enbt());
			obj = enbts;
			_type.Type = type;
			_type.IsSigned = true;
			_type.Length = LenType.Short;//utf-16
			_type.Endian = CurEndian;
		}
		public Enbt(string value)
		{
			obj = value;
			_type.Type = Type.sarray;
			_type.IsSigned = true;
			_type.Length = LenType.Short;//utf-16
			_type.Endian = CurEndian;
		}
		public Enbt(bool value)
		{
			obj = value;
			_type.Type = Type.bit;
			_type.IsSigned = false;
			_type.Length = LenType.Tiny;//utf-16
			_type.Endian = Endian.little;
		}
		public Enbt(sbyte value)
		{
			obj = value;
			_type = new() { Type = Type.integer, IsSigned = true, Length = LenType.Tiny, Endian = CurEndian };
		}
		public Enbt(short value)
		{
			obj = value;
			_type = new() { Type = Type.integer, IsSigned = true, Length = LenType.Short, Endian = CurEndian };
		}
		public Enbt(int value)
		{
			obj = value;
			_type = new() { Type = Type.integer, IsSigned = true, Length = LenType.Default, Endian = CurEndian };
		}
		public Enbt(long value)
		{
			obj = value;
			_type = new() { Type = Type.integer, IsSigned = true, Length = LenType.Tiny, Endian = CurEndian };
		}
		public Enbt(byte value)
		{
			obj = value;
			_type = new() { Type = Type.integer, IsSigned = false, Length = LenType.Tiny, Endian = CurEndian };
		}
		public Enbt(ushort value)
		{
			obj = value;
			_type = new() { Type = Type.integer, IsSigned = false, Length = LenType.Short, Endian = CurEndian };
		}
		public Enbt(uint value)
		{
			obj = value;
			_type = new() { Type = Type.integer, IsSigned = false, Length = LenType.Default, Endian = CurEndian };
		}
		public Enbt(ulong value)
		{
			obj = value;
			_type = new() { Type = Type.integer, IsSigned = false, Length = LenType.Tiny, Endian = CurEndian };
		}
		public Enbt(Guid value)
		{
			obj = value;
			_type = new() { Type = Type.uuid, IsSigned = false, Length = LenType.Tiny, Endian = CurEndian };
		}
		public Enbt(List<string> value)
		{
			List<Enbt> tok = new();
			foreach (string s in value)
				tok.Add(new Enbt(s));
			obj = tok;
			_type = new() { Type = Type.array, IsSigned = true, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(List<bool> value)
		{
			List<Enbt> tok = new();
			foreach (bool s in value)
				tok.Add(new Enbt(s));
			obj = tok;
			_type = new() { Type = Type.array, IsSigned = true, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(List<sbyte> value)
		{
			List<Enbt> tok = new();
			foreach (sbyte s in value)
				tok.Add(new Enbt(s));
			obj = tok;
			_type = new() { Type = Type.array, IsSigned = true, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(List<short> value)
		{
			List<Enbt> tok = new();
			foreach (sbyte s in value)
				tok.Add(new Enbt(s));
			obj = tok;
			_type = new() { Type = Type.array, IsSigned = true, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(List<int> value)
		{
			List<Enbt> tok = new();
			foreach (int s in value)
				tok.Add(new Enbt(s));
			obj = tok;
			_type = new() { Type = Type.array, IsSigned = true, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(List<long> value)
		{
			List<Enbt> tok = new();
			foreach (long s in value)
				tok.Add(new Enbt(s));
			obj = tok;
			_type = new() { Type = Type.array, IsSigned = true, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(List<byte> value)
		{
			List<Enbt> tok = new();
			foreach (byte s in value)
				tok.Add(new Enbt(s));
			obj = tok;
			_type = new() { Type = Type.array, IsSigned = true, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(List<ushort> value)
		{
			List<Enbt> tok = new();
			foreach (ushort s in value)
				tok.Add(new Enbt(s));
			obj = tok;
			_type = new() { Type = Type.array, IsSigned = true, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(List<uint> value)
		{
			List<Enbt> tok = new();
			foreach (uint s in value)
				tok.Add(new Enbt(s));
			obj = tok;
			_type = new() { Type = Type.array, IsSigned = true, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(List<ulong> value)
		{
			List<Enbt> tok = new();
			foreach (ulong s in value)
				tok.Add(new Enbt(s));
			obj = tok;
			_type = new() { Type = Type.array, IsSigned = true, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(List<Guid> value)
		{
			List<Enbt> tok = new();
			foreach (Guid s in value)
				tok.Add(new Enbt(s));
			obj = tok;
			_type = new() { Type = Type.array, IsSigned = true, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(List<Enbt> value,bool add_values = false)
		{
            if (add_values)
			{
				int add = value.Capacity;
				while (add-- > 0)
					value.Add(new Enbt());
			}
			obj = value;
			_type = new() { Type = Type.darray, IsSigned = true, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(Dictionary<string,string> value)
		{
			Dictionary<string, Enbt> tok = new();
            foreach (var s in value)
				tok.Add(s.Key,new Enbt(s.Value));
			obj = tok;
			_type = new() { Type = Type.compound, IsSigned = false, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(Dictionary<string, bool> value)
		{
			Dictionary<string, Enbt> tok = new();
			foreach (var s in value)
				tok.Add(s.Key, new Enbt(s.Value));
			obj = tok;
			_type = new() { Type = Type.compound, IsSigned = false, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(Dictionary<string, sbyte> value)
		{
			Dictionary<string, Enbt> tok = new();
			foreach (var s in value)
				tok.Add(s.Key, new Enbt(s.Value));
			obj = tok;
			_type = new() { Type = Type.compound, IsSigned = false, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(Dictionary<string, short> value)
		{
			Dictionary<string, Enbt> tok = new();
			foreach (var s in value)
				tok.Add(s.Key, new Enbt(s.Value));
			obj = tok;
			_type = new() { Type = Type.compound, IsSigned = false, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(Dictionary<string, int> value)
		{
			Dictionary<string, Enbt> tok = new();
			foreach (var s in value)
				tok.Add(s.Key, new Enbt(s.Value));
			obj = tok;
			_type = new() { Type = Type.compound, IsSigned = false, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(Dictionary<string, long> value)
		{
			Dictionary<string, Enbt> tok = new();
			foreach (var s in value)
				tok.Add(s.Key, new Enbt(s.Value));
			obj = tok;
			_type = new() { Type = Type.compound, IsSigned = false, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(Dictionary<string, byte> value)
		{
			Dictionary<string, Enbt> tok = new();
			foreach (var s in value)
				tok.Add(s.Key, new Enbt(s.Value));
			obj = tok;
			_type = new() { Type = Type.compound, IsSigned = false, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(Dictionary<string, ushort> value)
		{
			Dictionary<string, Enbt> tok = new();
			foreach (var s in value)
				tok.Add(s.Key, new Enbt(s.Value));
			obj = tok;
			_type = new() { Type = Type.compound, IsSigned = false, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(Dictionary<string, uint> value)
		{
			Dictionary<string, Enbt> tok = new();
			foreach (var s in value)
				tok.Add(s.Key, new Enbt(s.Value));
			obj = tok;
			_type = new() { Type = Type.compound, IsSigned = false, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(Dictionary<string, ulong> value)
		{
			Dictionary<string, Enbt> tok = new();
			foreach (var s in value)
				tok.Add(s.Key, new Enbt(s.Value));
			obj = tok;
			_type = new() { Type = Type.compound, IsSigned = false, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(Dictionary<string, Guid> value)
		{
			Dictionary<string, Enbt> tok = new();
			foreach (var s in value)
				tok.Add(s.Key, new Enbt(s.Value));
			obj = tok;
			_type = new() { Type = Type.compound, IsSigned = false, Length = LenType.Long, Endian = CurEndian };
		}
		public Enbt(Dictionary<string, Enbt> value)
		{
			obj = value;
			_type = new() { Type = Type.compound, IsSigned = false, Length = LenType.Long, Endian = CurEndian };
		}
		

		public Enbt(Enbt? optional)
		{
			_type.Type = Type.optional;
			_type.IsSigned = optional != null;
			_type.Length = LenType.Long;
			_type.Endian = Endian.big;
			obj = optional;
		}
		public TypeId GetTypeId()
        {
			return new TypeId() { Full = _type.Full };
		}
		public Enbt this[int index]
		{
			get
			{
				if (obj is List<Enbt> list)
					return list[index];
				else
					throw new InvalidOperationException("int index allowed for array types");
			}
			set
			{
				if (obj is List<Enbt> list)
					list[index] = value;
				else
					throw new InvalidOperationException("int index allowed for array types");
			}
		}
		public Enbt this[string index] 
		{
			get
			{
				if (obj is Dictionary<string, Enbt> dic)
					return dic[index];
				else
					throw new InvalidOperationException("string index allowed for compoud types");
			}
            set
            {
				if (obj is Dictionary<string, Enbt> dic)
					dic[index] = value;
				else
					throw new InvalidOperationException("string index allowed for compoud types");
			}
		}
		public T? GetAs<T>() => obj != null ? (T?)obj : default;
		public IEnumerator<Enbt> GetEnumerator()
		{
			if (obj == null)
				throw new NullReferenceException();

			if (obj is List<Enbt> list)
				return list.GetEnumerator();
			else if (obj is Dictionary<string, Enbt> dic)
				return new UnkeyedEnumerator(dic);
			throw new InvalidOperationException("IEnumerator<EnbtToken> can be recuived from enumerable types");
		}
		public Dictionary<string, Enbt>.Enumerator GetCompoudEnumerator()
		{
			if (obj == null)
				throw new NullReferenceException();
			if (obj is Dictionary<string, Enbt> dic)
				return dic.GetEnumerator();
			throw new InvalidOperationException("Dictionary<string, EnbtToken>.Enumerator can be recuived from compoud type");
		}
		IEnumerator IEnumerable.GetEnumerator()
		{
			if (obj == null)
				throw new NullReferenceException();
			if (obj is List<Enbt> list)
				return list.GetEnumerator();
			else if (obj is Dictionary<string, Enbt> dic)
				return dic.GetEnumerator();
			throw new InvalidOperationException("IEnumerator<EnbtToken> can be recuived from enumerable types");
		}


        public override string ToString()
		{
			if(_type.Type == Type.none)
					return "";
			if (_type.Type == Type.optional && obj == null)
				return "empty";
			if (obj == null)
				return $"null";
			switch (_type.Type)
			{
				case Type.integer:
				case Type.floating:
				case Type.var_integer:
				case Type.uuid:
				case Type.optional:
				case Type.bit:
					return $"{obj}";
				case Type.sarray:
					if(_type.Length == LenType.Default)
                    {
						return $"({obj})";
                    }
					else
                    {
						StringBuilder sb = new();
						sb.Append('(');
						bool not_first_it = false;
						if (_type.IsSigned)
						{
							switch (_type.Length)
							{
								case LenType.Tiny:
									foreach (var it in (sbyte[])obj)
									{
										if (not_first_it) sb.Append(", ");
										not_first_it = true;
										sb.Append(it);
									}
									break;
								case LenType.Default:
									foreach (var it in (int[])obj)
									{
										if (not_first_it) sb.Append(", ");
										not_first_it = true;
										sb.Append(it);
									}
									break;
								case LenType.Long:
									foreach (var it in (long[])obj)
									{
										if (not_first_it) sb.Append(", ");
										not_first_it = true;
										sb.Append(it);
									}
									break;
							}
                        }
                        else
                        {
							switch (_type.Length)
							{
								case LenType.Tiny:
									foreach (var it in (byte[])obj)
									{
										if (not_first_it) sb.Append(", ");
										not_first_it = true;
										sb.Append(it);
									}
									break;
								case LenType.Default:
									foreach (var it in (uint[])obj)
									{
										if (not_first_it) sb.Append(", ");
										not_first_it = true;
										sb.Append(it);
									}
									break;
								case LenType.Long:
									foreach (var it in (ulong[])obj)
									{
										if (not_first_it) sb.Append(", ");
										not_first_it = true;
										sb.Append(it);
									}
									break;
							}
						}
						sb.Append(')');
						return sb.ToString();
					}
				case Type.darray:
				case Type.array:
					{
						StringBuilder sb = new();
						bool not_first_it = false;
						sb.Append('[');
						foreach (var it in (List<Enbt>)obj)
						{
							if (not_first_it) sb.Append(", ");
							not_first_it = true;
							sb.Append(it);
						}
						sb.Append(']');
						return sb.ToString();
					}
				case Type.structure:
					{
						StringBuilder sb = new();
						bool not_first_it = false;
						sb.Append('{');
						foreach (var it in (List<Enbt>)obj)
						{
							if (not_first_it) sb.Append(", ");
							not_first_it = true;
							sb.Append(it);
						}
						sb.Append('}');
						return sb.ToString();
					}
				case Type.compound:
					{
						StringBuilder sb = new();
						bool not_first_it = false;
						sb.Append('{');
						foreach (var it in (Dictionary<string,Enbt>)obj)
						{
							if (not_first_it) sb.Append(", ");
							not_first_it = true;
							sb.Append($"\"{it.Key}\":");
							sb.Append(it.Value);
						}
						sb.Append('}');
						return sb.ToString();
					}
				default:
					throw new NotImplementedException();
			}

		}

        public object Clone()
			=> new Enbt(_type, obj);
        

        public static implicit operator Enbt(bool v) => new(v);
		public static implicit operator Enbt(sbyte v) => new(v);
		public static implicit operator Enbt(short v) => new(v);
		public static implicit operator Enbt(int v) => new(v);
		public static implicit operator Enbt(long v) => new(v);
		public static implicit operator Enbt(ushort v) => new(v);
		public static implicit operator Enbt(uint v) => new(v);
		public static implicit operator Enbt(ulong v) => new(v);
		public static implicit operator Enbt(string v) => new(v);
		public static implicit operator Enbt(Guid v) => new(v);
		public static implicit operator Enbt(List<bool> v) => new(v);
		public static implicit operator Enbt(List<sbyte> v) => new(v);
		public static implicit operator Enbt(List<short> v) => new(v);
		public static implicit operator Enbt(List<int> v) => new(v);
		public static implicit operator Enbt(List<long> v) => new(v);
		public static implicit operator Enbt(List<ushort> v) => new(v);
		public static implicit operator Enbt(List<uint> v) => new(v);
		public static implicit operator Enbt(List<ulong> v) => new(v);
		public static implicit operator Enbt(List<string> v) => new(v);
		public static implicit operator Enbt(List<Guid> v) => new(v);
		public static implicit operator Enbt(List<Enbt> v) => new(v, true);
		public static implicit operator Enbt(Dictionary<string,bool> v) => new(v);
		public static implicit operator Enbt(Dictionary<string,sbyte> v) => new(v);
		public static implicit operator Enbt(Dictionary<string,short> v) => new(v);
		public static implicit operator Enbt(Dictionary<string,int> v) => new(v);
		public static implicit operator Enbt(Dictionary<string,long> v) => new(v);
		public static implicit operator Enbt(Dictionary<string,ushort> v) => new(v);
		public static implicit operator Enbt(Dictionary<string,uint> v) => new(v);
		public static implicit operator Enbt(Dictionary<string,ulong> v) => new(v);
		public static implicit operator Enbt(Dictionary<string,string> v) => new(v);
		public static implicit operator Enbt(Dictionary<string, Guid> v) => new(v);
		public static implicit operator Enbt(Dictionary<string, Enbt> v) => new(v);
	}


	//TO.DO
	// implement EnbtWriter
	// implement EnbtReader
}
