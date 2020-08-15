using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct FlipperStaticData : IComponentData
	{
		public float Inertia;
		public float AngleStart;
		public float AngleEnd;
		public float Strength;
		public float ReturnRatio;
		public float TorqueDamping;
		public float TorqueDampingAngle;
		public float RampUpSpeed;

		// only used in hit, probably split
		public float EndRadius;
		public float FlipperRadius;

	}
}
