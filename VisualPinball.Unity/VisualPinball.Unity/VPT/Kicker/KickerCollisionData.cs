using Unity.Entities;

namespace VisualPinball.Unity.VPT.Kicker
{
	public struct KickerCollisionData : IComponentData
	{
		public bool HasBall;
		public Entity LastCapturedBallEntity;

	}
}
