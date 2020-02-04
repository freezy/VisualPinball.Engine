using System;
using System.IO;
using VisualPinball.Engine.VPT.Table;

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

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			if (Type == typeof(byte[])) {
				var bytes = GetValue(obj) as byte[];
				WriteStart(writer, bytes.Length, hashWriter);
				writer.Write(bytes);
				hashWriter?.Write(bytes);
			}
		}
	}
}
