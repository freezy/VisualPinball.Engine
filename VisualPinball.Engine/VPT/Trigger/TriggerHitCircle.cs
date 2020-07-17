using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Trigger
{
	public class TriggerHitCircle : HitCircle
	{
		public TriggerHitCircle(Vertex2D center, float radius, float zLow, float zHigh) : base(center, radius, zLow, zHigh, ItemType.Trigger)
		{
		}
	}
}
