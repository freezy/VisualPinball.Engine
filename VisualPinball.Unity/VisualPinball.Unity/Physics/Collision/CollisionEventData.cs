using Unity.Entities;
using Unity.Mathematics;

namespace VisualPinball.Unity.Physics.Collision
{
	public struct CollisionEventData : IComponentData
	{
		public float HitTime;
		public float3 HitNormal;
		public float HitDistance;
		public float HitOrgNormalVelocity;
		public bool IsContact;

		public void Set(CollisionEventData newColl)
		{
			HitTime = newColl.HitTime;
			HitNormal = newColl.HitNormal;
			HitDistance = newColl.HitDistance;
			HitOrgNormalVelocity = newColl.HitOrgNormalVelocity;
			IsContact = newColl.IsContact;
		}
	}
}
