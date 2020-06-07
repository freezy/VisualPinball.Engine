using Unity.Entities;

namespace VisualPinball.Unity.VPT.Bumper
{
	public struct BumperStaticData : IComponentData
	{
		public float Force;
		public float Threshold;
		public bool HitEvent;
		public Entity RingEntity;
		public Entity SkirtEntity;
	}
}
