using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct FlipperVelocityData : IComponentData
	{
		public bool Direction;
		public float CurrentTorque;
		public float ContactTorque;
		public bool IsInContact;
		public float AngularAcceleration;
	}
}
