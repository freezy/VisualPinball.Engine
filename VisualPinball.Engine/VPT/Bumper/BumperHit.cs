using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Bumper
{
	public class BumperHit : HitCircle
	{
		public BumperHit(Vertex2D center, float radius, float zLow, float zHigh) : base(center, radius, zLow, zHigh)
		{
		}
	}
}
