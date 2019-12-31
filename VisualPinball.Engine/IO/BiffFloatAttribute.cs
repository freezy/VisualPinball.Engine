using System;
using System.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.IO
{
	public class BiffFloatAttribute : BiffAttribute
	{
		public int QuantizedUnsignedBits = -1;
		public bool AsPercent = false;

		public BiffFloatAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			if (Type == typeof(float)) {
				SetValue(obj, ReadFloat(reader));

			} else if (Type == typeof(float[])) {
				var arr = GetValue(obj) as float[];
				if (Count > 1) {
					for (var i = 0; i < Count; i++) {
						arr[i] = ReadFloat(reader);
					}
				} else {
					arr[Index] = ReadFloat(reader);
				}
			}
		}

		private float ReadFloat(BinaryReader reader)
		{
			var f = QuantizedUnsignedBits > 0
				? DequantizeUnsigned(QuantizedUnsignedBits, reader.ReadInt32())
				: reader.ReadSingle();

			if (AsPercent) {
				return f * 100f;
			}

			return f;
		}

		public static float DequantizeUnsigned(int bits, int i)
		{
			var n = (1 << bits) - 1;
			return MathF.Min(i / (float) n, 1.0f);
		}
	}
}
