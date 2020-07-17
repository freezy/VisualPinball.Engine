using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Trigger
{
	public class TriggerHitLineSeg : LineSeg
	{
		public TriggerHitLineSeg(Vertex2D p1, Vertex2D p2, float zLow, float zHigh) : base(p1, p2, zLow, zHigh, ItemType.Trigger)
		{
		}
	}
}
