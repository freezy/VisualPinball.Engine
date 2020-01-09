using System;
using System.IO;

namespace VisualPinball.Engine.IO
{
	public class BiffIntAttribute : BiffAttribute
	{
		public BiffIntAttribute(string name) : base(name) { }

		public int Min = int.MinValue;
		public int Max = int.MaxValue;

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			ParseValue(obj, reader, len, ReadInt);
			ParseValue(obj, reader, len, ReadUInt);
		}

		private int ReadInt(BinaryReader reader, int len)
		{
			var i = reader.ReadInt32();
			if (i > Max) {
				i = Max;
			}

			if (i < Min) {
				i = Min;
			}

			return i;
		}

		private uint ReadUInt(BinaryReader reader, int len)
		{
			return reader.ReadUInt32();
		}

	}
}
