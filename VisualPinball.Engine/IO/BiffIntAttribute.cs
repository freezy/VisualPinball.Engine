using System;
using System.IO;

namespace VisualPinball.Engine.IO
{
	public class BiffIntAttribute : BiffAttribute
	{
		public BiffIntAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			ParseValue(obj, reader, len, ReadInt);
			ParseValue(obj, reader, len, ReadUInt);
		}

		private int ReadInt(BinaryReader reader, int len)
		{
			return reader.ReadInt32();
		}

		private uint ReadUInt(BinaryReader reader, int len)
		{
			return reader.ReadUInt32();
		}

	}
}
