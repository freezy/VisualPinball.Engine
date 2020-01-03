using System;
using System.IO;

namespace VisualPinball.Engine.IO
{
	public class BiffByteAttribute : BiffAttribute
	{
		public BiffByteAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			if (Type == typeof(byte[])) {
				SetValue(obj, reader.ReadBytes(len));
			}
		}
	}
}
