using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity.VPT.Flipper
{
	public struct FlipperMovementData : IComponentData
	{
		public float Angle;
		public float AngleSpeed;
		public float AngularMomentum;
		public sbyte EnableRotateEvent;
		public quaternion BaseRotation;
		public long CurrentPhysicsTime;
		public long DebugRelTimeDelta;
	}
}
