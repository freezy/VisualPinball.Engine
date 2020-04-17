using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Unity.Physics.Collision;

namespace VisualPinball.Unity.VPT.Ball
{
	// todo split this into at least 2 components
	public struct BallData : IComponentData
	{
		public float3 Position;
		public float3 Velocity;
		public float3 AngularVelocity;
		public float3 AngularMomentum;
		public float Radius;
		public float Mass;
		public bool IsFrozen;

		public Aabb Aabb {
			get {
				var vl = math.length(Velocity) + Radius + 0.05f; //!! 0.05f = paranoia
				return new Aabb(
					Position.x - vl,
					Position.x + vl,
					Position.y - vl,
					Position.y + vl,
					Position.z - vl,
					Position.z + vl
				);
			}
		}

		public float CollisionRadiusSqr {
			get {
				var v1 = math.length(Velocity) + Radius + 0.05f;
				return v1 * v1;
			}
		}

		public float3 SurfaceVelocity(float3 surfP)
		{
			// linear velocity plus tangential velocity due to rotation
			return Velocity + math.cross(AngularVelocity, surfP);
		}

		public void ApplySurfaceImpulse(float3 rotI, float3 impulse)
		{
			Velocity += impulse / Mass;
			AngularMomentum += rotI;
		}
	}
}
