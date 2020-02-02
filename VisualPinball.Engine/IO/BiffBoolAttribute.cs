using System;
using System.IO;

namespace VisualPinball.Engine.IO
{
	public class BiffBoolAttribute : BiffAttribute
	{
		public BiffBoolAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			ParseValue(obj, reader, len, ReadBool);
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer)
		{
			WriteValue<TItem, bool>(obj, writer, WriteBool);
		}

		private static bool ReadBool(BinaryReader reader, int len)
		{
			return reader.ReadInt32() > 0;
		}

		private static void WriteBool(BinaryWriter writer, bool value)
		{
			writer.Write(value ? 1 : 0);
		}
	}
}
