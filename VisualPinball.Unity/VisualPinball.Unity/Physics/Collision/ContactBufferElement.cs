using Unity.Entities;

namespace VisualPinball.Unity
{
	[InternalBufferCapacity(1)]
	public struct ContactBufferElement : IBufferElementData
	{
		public CollisionEventData CollisionEvent;
		public int ColliderId;
		public Entity ColliderEntity;

		public ContactBufferElement(int colliderId, CollisionEventData collEvent)
		{
			CollisionEvent = collEvent;
			ColliderId = colliderId;
			ColliderEntity = Entity.Null;
		}

		public ContactBufferElement(Entity colliderEntity, CollisionEventData collEvent)
		{
			CollisionEvent = collEvent;
			ColliderId = -1;
			ColliderEntity = colliderEntity;
		}
	}

}
