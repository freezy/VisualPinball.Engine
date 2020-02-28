using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity.Physics.Flipper
{
	public struct FlipperMovementData : IComponentData
	{
		public float Angle;
		public float AngleSpeed;
		public float AngularMomentum;
		public sbyte EnableRotateEvent;
		public quaternion BaseRotation;
	}
}
