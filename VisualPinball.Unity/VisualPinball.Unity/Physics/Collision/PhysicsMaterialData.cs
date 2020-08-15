using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct PhysicsMaterialData : IComponentData
	{
		public float Elasticity;
		public float ElasticityFalloff;
		public float Friction;
		public float Scatter;
	}
}
