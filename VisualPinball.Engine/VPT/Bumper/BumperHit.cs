using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Bumper
{
	public class BumperHit : HitCircle
	{
		public BumperHit(BumperData data, float height) : base(data.Center, data.Radius, height, height + data.HeightScale, ItemType.Bumper)
		{
			FireEvents = data.HitEvent;
			Threshold = data.Threshold;
		}
	}
}
