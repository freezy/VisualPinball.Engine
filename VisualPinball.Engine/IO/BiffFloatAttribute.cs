using System;
using System.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.IO
{
	public class BiffFloatAttribute : BiffAttribute
	{
		public int QuantizedUnsignedBits = -1;
		public bool AsInt = false;

		public BiffFloatAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			if (!AsInt) {
				ParseValue(obj, reader, len, ReadFloat);
			} else {
				ParseValue(obj, reader, len, ReadInt);
			}
		}

		private float ReadFloat(BinaryReader reader, int len)
		{
			var f = QuantizedUnsignedBits > 0
				? DequantizeUnsigned(QuantizedUnsignedBits, reader.ReadInt32())
				: reader.ReadSingle();

			return f;
		}

		private int ReadInt(BinaryReader reader, int len)
		{
			return (int) ReadFloat(reader, len);
		}

		public static float DequantizeUnsigned(int bits, int i)
		{
			var n = (1 << bits) - 1;
			return MathF.Min(i / (float) n, 1.0f);
		}
	}
}
