using System.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.IO
{
	public class BiffDragPointAttribute : BiffAttribute
	{
		public BiffDragPointAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			ParseValue(obj, reader, len, ReadDragPoint);
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer)
		{
			WriteValue<TItem, DragPoint>(obj, writer, WriteDragpoint);
		}

		private static void WriteDragpoint(BinaryWriter writer, DragPoint value)
		{
			value.Write(writer);
		}

		private static DragPoint ReadDragPoint(BinaryReader reader, int len)
		{
			return new DragPoint(reader);
		}
	}
}
