using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct TriggerAnimationData : IComponentData
	{
		public bool HitEvent;
		public bool UnHitEvent;

		public float TimeMsec;
		public bool DoAnimation;
		public bool MoveDown;
	}
}
