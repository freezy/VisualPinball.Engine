using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct GateMovementData : IComponentData
	{
		public float Angle;
		public float AngleSpeed;
		public bool ForcedMove;
		public bool IsOpen;
	}
}
