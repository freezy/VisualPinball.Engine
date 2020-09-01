// ReSharper disable CommentTypo

using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class LineSeg : HitObject
	{
		public readonly Vertex2D V1;
		public readonly Vertex2D V2;
		public readonly Vertex2D Normal = new Vertex2D();
		public float Length;

		public LineSeg(Vertex2D p1, Vertex2D p2, float zLow, float zHigh, ItemType objType) : base(objType)
		{
			V1 = p1;
			V2 = p2;
			HitBBox.ZLow = zLow;
			HitBBox.ZHigh = zHigh;
			CalcNormal();
			CalcHitBBox();
		}

		public LineSeg SetSeg(float x1, float y1, float x2, float y2)
		{
			V1.X = x1;
			V1.Y = y1;
			V2.X = x2;
			V2.Y = y2;
			CalcNormal().CalcHitBBox();
			return this;
		}

		public override void CalcHitBBox()
		{
			// Allow roundoff
			HitBBox.Left = MathF.Min(V1.X, V2.X);
			HitBBox.Right = MathF.Max(V1.X, V2.X);
			HitBBox.Top = MathF.Min(V1.Y, V2.Y);
			HitBBox.Bottom = MathF.Max(V1.Y, V2.Y);

			// zlow and zhigh were already set in constructor
		}

		private LineSeg CalcNormal()
		{
			var vT = new Vertex2D(V1.X - V2.X, V1.Y - V2.Y);

			// Set up line normal
			Length = vT.Length();
			var invLength = 1.0f / Length;
			Normal.Set(vT.Y * invLength, -vT.X * invLength);
			return this;
		}
	}
}
