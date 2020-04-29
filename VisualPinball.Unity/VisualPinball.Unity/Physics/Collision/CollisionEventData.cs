using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity.Physics.Collision
{
	public struct CollisionEventData : IComponentData
	{
		public float HitTime;
		public float3 HitNormal;
		public float HitDistance;
		public bool HitFlag;
		public float HitOrgNormalVelocity;
		public bool IsContact;

		public int ColliderId;
		public Entity ColliderEntity;

		public void Reset(float hitTime)
		{
			HitTime = hitTime;
			ColliderId = -1;
			ColliderEntity = Entity.Null;
			IsContact = false;
			HitFlag = false;
		}

		public void Set(int colliderId, CollisionEventData newColl)
		{
			ColliderId = colliderId;
			ColliderEntity = Entity.Null;

			Set(newColl);
		}

		public void Set(Entity colliderEntity, CollisionEventData newColl)
		{
			ColliderId = -1;
			ColliderEntity = colliderEntity;

			Set(newColl);
		}

		private void Set(CollisionEventData newColl)
		{
			HitTime = newColl.HitTime;
			HitNormal = newColl.HitNormal;
			HitDistance = newColl.HitDistance;
			HitFlag = newColl.HitFlag;
			HitOrgNormalVelocity = newColl.HitOrgNormalVelocity;
			IsContact = newColl.IsContact;
		}
	}
}
