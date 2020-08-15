using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct HitTargetAnimationData : IComponentData
	{
		public bool HitEvent;
		public uint TimeMsec;
		public uint TimeStamp;

		public bool MoveDown;
		public bool IsDropped;
		public bool MoveAnimation;
	}
}
