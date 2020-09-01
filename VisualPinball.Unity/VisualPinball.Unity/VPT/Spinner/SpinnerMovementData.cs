using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct SpinnerMovementData : IComponentData
	{
		public float Angle;
		public float AngleSpeed;
	}
}
