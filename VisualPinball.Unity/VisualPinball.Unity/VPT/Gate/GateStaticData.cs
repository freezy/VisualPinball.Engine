using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct GateStaticData : IComponentData
	{
		public float AngleMin;
		public float AngleMax;
		public float Height;
		public float GravityFactor;
		public float Damping;
		public bool TwoWay;
	}
}
