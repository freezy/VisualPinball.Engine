using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity
{
	public struct CollisionEventData : IComponentData
	{
		public float HitTime;
		public float3 HitNormal;
		public float2 HitVelocity;
		public float HitDistance;
		public bool HitFlag;
		public float HitOrgNormalVelocity;
		public bool IsContact;

		public int ColliderId;
		public Entity ColliderEntity;

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

		public void ClearCollider(float hitTime)
		{
			HitTime = hitTime;
			ColliderId = -1;
			ColliderEntity = Entity.Null;
		}


		public void ClearCollider()
		{
			ColliderId = -1;
			ColliderEntity = Entity.Null;
		}

		public bool HasCollider()
		{
			return ColliderId > -1 || ColliderEntity != Entity.Null;
		}
	}
}
