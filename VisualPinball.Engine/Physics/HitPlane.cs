using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class HitPlane : HitObject
	{
		public readonly Vertex3D Normal;
		public readonly float D;

		public HitPlane(Vertex3D normal, float d) : base(ItemType.Table)
		{
			Normal = normal;
			D = d;
		}

		public override void CalcHitBBox()
		{
			// plane's not a box (i assume)
		}
	}
}
