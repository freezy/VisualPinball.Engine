using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct SpinnerMovementData : IComponentData
	{
		public float Angle;
		public float AngleSpeed;
	}
}
