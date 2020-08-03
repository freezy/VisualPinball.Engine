using System.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

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

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			if (!AsInt) {
				WriteValue<TItem, float>(obj, writer, WriteFloat, hashWriter);
			} else {
				WriteValue<TItem, int>(obj, writer, WriteFloat, hashWriter);
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

		private void WriteFloat(BinaryWriter writer, float value)
		{
			if (QuantizedUnsignedBits > 0) {
				writer.Write(QuantizeUnsigned(QuantizedUnsignedBits, value));
			} else {
				writer.Write(value);
			}
		}

		private static void WriteFloat(BinaryWriter writer, int value)
		{
			writer.Write((float)value);
		}

		public static float DequantizeUnsigned(int bits, int i)
		{
			var n = (1 << bits) - 1;
			return MathF.Min(i / (float) n, 1.0f);
		}

		public static uint QuantizeUnsigned(int bits, float x)
		{
			var n = (1 << bits) - 1;
			var np1 = 1 << bits;
			return System.Math.Min((uint)(x * np1), (uint)n);
		}
	}
}
