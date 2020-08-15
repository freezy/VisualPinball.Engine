using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct KickerCollisionData : IComponentData
	{
		public bool HasBall;
		public Entity LastCapturedBallEntity;

	}
}
