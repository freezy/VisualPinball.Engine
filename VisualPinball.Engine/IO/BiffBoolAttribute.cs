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

		private bool ReadBool(BinaryReader reader, int len)
		{
			return reader.ReadInt32() > 0;
		}
	}
}
