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
			WriteValue<TItem, DragPoint>(obj, writer, (w, v) => WriteDragpoint(w, v, hashWriter), hashWriter);
		}

		private static void WriteDragpoint(BinaryWriter writer, DragPoint value, HashWriter hashWriter)
		{
			value.Write(writer, hashWriter);
		}

		private static DragPoint ReadDragPoint(BinaryReader reader, int len)
		{
			return new DragPoint(reader);
		}
	}
}
