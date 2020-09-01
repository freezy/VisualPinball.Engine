using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Physics
{
	public class HitCircle : HitObject
	{
		public readonly Vertex2D Center;
		public readonly float Radius;

		public HitCircle(Vertex2D center, float radius, float zLow, float zHigh, ItemType itemType, IItem item) : base(itemType, item)
		{
			Center = center;
			Radius = radius;
			HitBBox.ZLow = zLow;
			HitBBox.ZHigh = zHigh;
		}

		public override void CalcHitBBox()
		{
			// Allow roundoff
			HitBBox.Left = Center.X - Radius;
			HitBBox.Right = Center.X + Radius;
			HitBBox.Top = Center.Y - Radius;
			HitBBox.Bottom = Center.Y + Radius;
			// zlow & zhigh already set in ctor
		}
	}
}
