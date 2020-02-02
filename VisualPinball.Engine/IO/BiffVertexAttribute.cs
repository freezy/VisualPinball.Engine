using System;
using System.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.IO
{
	public class BiffVertexAttribute : BiffAttribute
	{
		public BiffVertexAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			ParseValue(obj, reader, len, ReadVertex2D);
			ParseValue(obj, reader, len, ReadVertex3D);
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer)
		{
			if (Type == typeof(Vertex2D)) {
				WriteValue<TItem, Vertex2D>(obj, writer, WriteVertex2D);

			} else if (Type == typeof(Vertex3D)) {
				WriteValue<TItem, Vertex3D>(obj, writer, WriteVertex3D);

			} else {
				throw new InvalidOperationException("Unknown type for [BiffVertex] on field \"" + Name + "\".");
			}
		}

		private static Vertex2D ReadVertex2D(BinaryReader reader, int len)
		{
			return new Vertex2D(reader, len);
		}

		private static Vertex3D ReadVertex3D(BinaryReader reader, int len)
		{
			return new Vertex3D(reader, len);
		}

		private void WriteVertex2D(BinaryWriter writer, Vertex2D value)
		{
			WriteStart(writer, 8);
			writer.Write(value.X);
			writer.Write(value.Y);
		}

		private void WriteVertex3D(BinaryWriter writer, Vertex3D value)
		{
			WriteStart(writer, 12);
			writer.Write(value.X);
			writer.Write(value.Y);
			writer.Write(value.Z);
		}
	}
}
