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

		public void SetCollider(int colliderId)
		{
			ColliderId = colliderId;
			ColliderEntity = Entity.Null;
		}

		public void SetCollider(Entity colliderEntity)
		{
			ColliderId = -1;
			ColliderEntity = colliderEntity;
		}

		public void Set(CollisionEventData newCollEvent)
		{
			HitTime = newCollEvent.HitTime;
			HitNormal = newCollEvent.HitNormal;
			HitDistance = newCollEvent.HitDistance;
			HitFlag = newCollEvent.HitFlag;
			HitOrgNormalVelocity = newCollEvent.HitOrgNormalVelocity;
			IsContact = newCollEvent.IsContact;
		}
	}
}
