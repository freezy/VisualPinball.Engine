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

		private Vertex2D ReadVertex2D(BinaryReader reader, int len)
		{
			return new Vertex2D(reader);
		}

		private Vertex3D ReadVertex3D(BinaryReader reader, int len)
		{
			return new Vertex3D(reader, len);
		}
	}
}
