using System;

namespace VisualPinball.Engine.Math
{
	public class RenderVertex2D : Vertex2D, IRenderVertex
	{
		public IRenderVertex Set(Vertex3D v)
		{
			base.Set(v.X, v.Y);
			return this;
		}

		public bool Smooth { get; set; }
		public bool IsSlingshot { get; set; }
		public bool IsControlPoint { get; set; }

		public RenderVertex2D() { }
		public RenderVertex2D(float x, float y) : base(x, y) { }
	}

	public class RenderVertex3D : Vertex3D, IRenderVertex
	{
		public new IRenderVertex Set(Vertex3D v)
		{
			base.Set(v);
			return this;
		}

		public bool Smooth { get; set; }
		public bool IsSlingshot { get; set; }
		public bool IsControlPoint { get; set; }

		public RenderVertex3D() { }
		public RenderVertex3D(float x, float y, float z) : base(x, y, z) { }
	}

	public interface IRenderVertex
	{
		IRenderVertex Set(Vertex3D v);

		float GetX();
		float GetY();

		bool Smooth { get; set; }
		bool IsSlingshot { get; set; }
		bool IsControlPoint { get; set; }
	}
	//
	// public interface IVertex {
	// 	IVertex Clone();
	// 	IVertex Sub(IVertex v);
	// 	float Length();
	// }
}
