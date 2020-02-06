using System.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.IO
{
	public class BiffDragPointAttribute : BiffAttribute
	{
		public BiffDragPointAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			ParseValue(obj, reader, len, ReadDragPoint);
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			WriteValue<TItem, DragPointData>(obj, writer, (w, v) => WriteDragPoint(w, v, hashWriter), hashWriter, x => 0);
		}

		private static void WriteDragPoint(BinaryWriter writer, BiffData value, HashWriter hashWriter)
		{
			value.Write(writer, hashWriter);
		}

		private static DragPointData ReadDragPoint(BinaryReader reader, int len)
		{
			return new DragPointData(reader);
		}
	}
}
