using System.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.IO
{
	public class BiffColorAttribute : BiffAttribute
	{
		/// <summary>
		/// For colors, this defines how the integer is encoded.
		/// </summary>
		public ColorFormat ColorFormat = ColorFormat.Bgr;

		public BiffColorAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			ParseValue(obj, reader, len, ReadColor);
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			WriteValue<TItem, Color>(obj, writer, WriteColor, hashWriter);
		}

		private static void WriteColor(BinaryWriter writer, Color value)
		{
			writer.Write(value.Bgr);
		}

		private Color ReadColor(BinaryReader reader, int len)
		{
			return new Color(reader.ReadInt32(), ColorFormat);
		}
	}
}
