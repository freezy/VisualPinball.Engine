using Unity.Entities;

namespace VisualPinball.Unity.VPT.Spinner
{
	public struct SpinnerMovementData : IComponentData
	{
		public float Angle;
		public float AngleSpeed;
	}
}
