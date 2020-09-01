using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct KickerCollisionData : IComponentData
	{
		public bool HasBall;
		public Entity LastCapturedBallEntity;

	}
}
