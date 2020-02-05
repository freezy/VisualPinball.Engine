using System;
using System.IO;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.IO
{
	public class BiffFontAttribute : BiffAttribute
	{
		public BiffFontAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			SetValue(obj, new Font(reader));
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			if (GetValue(obj) is Font font) {
				WriteStart(writer, 0, hashWriter);
				font.Write(writer, hashWriter);

			} else {
				throw new InvalidOperationException("Unknown type for [" + GetType().Name + "] on field \"" + Name + "\".");
			}
		}
	}
}
