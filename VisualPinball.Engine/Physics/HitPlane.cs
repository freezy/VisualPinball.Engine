using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Physics
{
	public class HitPlane : HitObject
	{
		public readonly Vertex3D Normal;
		public readonly float D;

		public HitPlane(Vertex3D normal, float d, IItem item) : base(ItemType.Table, item)
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
