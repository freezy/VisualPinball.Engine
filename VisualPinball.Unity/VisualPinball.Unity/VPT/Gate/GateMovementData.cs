using Unity.Entities;

namespace VisualPinball.Unity.VPT.Gate
{
	public struct GateMovementData : IComponentData
	{
		public float Angle;
		public float AngleSpeed;
		public bool ForcedMove;
		public bool IsOpen;
	}
}
