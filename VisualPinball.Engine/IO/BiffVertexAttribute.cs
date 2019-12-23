using System.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.IO
{
	public class BiffVertexAttribute : BiffAttribute
	{
		public BiffVertexAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			if (Type == typeof(Vertex3D)) {
				SetValue(obj, new Vertex3D(reader));

			} else if (Type == typeof(Vertex2D)) {
				SetValue(obj, new Vertex2D(reader));
			}
		}
	}
}
