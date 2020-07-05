using Unity.Entities;

namespace VisualPinball.Unity.Physics.Event
{
	public struct HitEvent
	{
		public Entity BallEntity;
		public Entity ItemEntity;
	}
}
