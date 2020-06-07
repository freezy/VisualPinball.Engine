using Unity.Entities;

namespace VisualPinball.Unity.VPT.Spinner
{
	public struct SpinnerStaticData : IComponentData
	{
		public float AngleMin;
		public float AngleMax;
		public float Height;
		public float Damping;
		public float Elasticity;
	}
}
