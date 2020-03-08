using Unity.Entities;

namespace VisualPinball.Unity.VPT.Flipper
{
	public struct FlipperVelocityData : IComponentData
	{
		public bool Direction;
		public float CurrentTorque;
		public float ContactTorque;
		public bool IsInContact;
		public float AngularAcceleration;
	}
}
