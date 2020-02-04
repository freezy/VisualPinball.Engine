using System;
using System.IO;
using VisualPinball.Engine.VPT.Table;

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

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			if (Type == typeof(int)) {
				WriteValue<TItem, int>(obj, writer, WriteInt, hashWriter);

			} else if (Type == typeof(uint)) {
				WriteValue<TItem, uint>(obj, writer, WriteUInt, hashWriter);

			} else {
				throw new InvalidOperationException("Unknown type for [BiffInt] on field \"" + Name + "\".");
			}
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

		private static uint ReadUInt(BinaryReader reader, int len)
		{
			return reader.ReadUInt32();
		}

		private static void WriteInt(BinaryWriter writer, int value)
		{
			writer.Write(value);
		}

		private static void WriteUInt(BinaryWriter writer, uint value)
		{
			writer.Write(value);
		}
	}
}
