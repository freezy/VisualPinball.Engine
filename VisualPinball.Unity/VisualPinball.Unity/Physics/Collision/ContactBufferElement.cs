using Unity.Entities;

namespace VisualPinball.Unity.Physics.Collision
{
	[InternalBufferCapacity(1)]
	public struct ContactBufferElement : IBufferElementData
	{
		public CollisionEventData CollisionEvent;
		public int ColliderId;
		public Entity ColliderEntity;
	}
}
