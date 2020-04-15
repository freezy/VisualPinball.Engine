using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Unity.Physics.Collision;

namespace VisualPinball.Unity.VPT.Ball
{
	public struct BallData : IComponentData
	{
		public float3 Position;
		public float3 Velocity;
		public float Radius;
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
	}
}
