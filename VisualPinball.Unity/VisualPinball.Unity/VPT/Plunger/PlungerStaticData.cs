using Unity.Entities;

namespace VisualPinball.Unity.VPT.Plunger
{
	public struct PlungerStaticData : IComponentData
	{
		public float MomentumXfer;
		public float ScatterVelocity;

		public Entity RodEntity;
		public Entity SpringEntity;
	}
}
