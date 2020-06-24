using Unity.Entities;

namespace VisualPinball.Unity.VPT.Trigger
{
	public struct TriggerAnimationData : IComponentData
	{
		public bool HitEvent;
		public bool UnHitEvent;
	}
}
