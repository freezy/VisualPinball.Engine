using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class HitPoint : HitObject
	{
		public readonly Vertex3D P;

		public HitPoint(Vertex3D p, ItemType itemType) : base(itemType)
		{
			P = p;
		}

		public override void CalcHitBBox()
		{
			HitBBox = new Rect3D(P.X, P.X, P.Y, P.Y, P.Z, P.Z);
		}
	}
}
