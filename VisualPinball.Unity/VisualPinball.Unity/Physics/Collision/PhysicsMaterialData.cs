using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct PhysicsMaterialData : IComponentData
	{
		public float Elasticity;
		public float ElasticityFalloff;
		public float Friction;
		public float Scatter;
	}
}
