using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct SpinnerStaticData : IComponentData
	{
		public float AngleMin;
		public float AngleMax;
		public float Height;
		public float Damping;
		public float Elasticity;
	}
}
