using Unity.Entities;

namespace VisualPinball.Unity.Physics.Collision
{
	public struct CollisionEventData : IComponentData
	{
		public float hitTime;
		public bool isContact;

		public void Set(CollisionEventData newColl)
		{
			hitTime = newColl.hitTime;
			isContact = newColl.isContact;
		}
	}
}
