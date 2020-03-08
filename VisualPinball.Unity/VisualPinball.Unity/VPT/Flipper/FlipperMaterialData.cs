using Unity.Entities;

namespace VisualPinball.Unity.VPT.Flipper
{
	public struct FlipperMaterialData : IComponentData
	{
		public float Inertia;
		public float AngleStart;
		public float AngleEnd;
		public float Strength;
		public float ReturnRatio;
		public float TorqueDamping;
		public float TorqueDampingAngle;
		public float RampUpSpeed;
	}
}
