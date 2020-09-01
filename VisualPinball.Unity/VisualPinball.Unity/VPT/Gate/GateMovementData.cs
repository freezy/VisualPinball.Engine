using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct GateMovementData : IComponentData
	{
		public float Angle;
		public float AngleSpeed;
		public bool ForcedMove;
		public bool IsOpen;
	}
}
