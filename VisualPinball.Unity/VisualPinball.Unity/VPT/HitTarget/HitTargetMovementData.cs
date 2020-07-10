using Unity.Entities;

namespace VisualPinball.Unity.VPT.HitTarget
{
	public struct HitTargetMovementData : IComponentData
	{
		public float ZOffset;
		public float XRotation;
	}
}
