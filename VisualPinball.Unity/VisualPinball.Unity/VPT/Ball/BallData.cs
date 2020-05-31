using System;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Unity.Physics.Collision;

namespace VisualPinball.Unity.VPT.Ball
{
	// todo split this into at least 2 components
	public struct BallData : IComponentData
	{
		public int Id;
		public float3 Position;
		public float3 Velocity;
		public float3 AngularVelocity;
		public float3 AngularMomentum;
		public float3x3 Orientation;
		public float Radius;
		public float Mass;
		public bool IsFrozen;
		public int RingCounterOldPos;

		public Aabb Aabb {
			get {
				var vl = math.length(Velocity) + Radius + 0.05f; // 0.05f = paranoia
				return new Aabb(
					-1,
					Position.x - vl,
					Position.x + vl,
					Position.y - vl,
					Position.y + vl,
					Position.z - vl,
					Position.z + vl
				);
			}
		}

		public Aabb GetAabb(Entity entity) {
			var vl = math.length(Velocity) + Radius + 0.05f; // 0.05f = paranoia
			return new Aabb(
				entity,
				Position.x - vl,
				Position.x + vl,
				Position.y - vl,
				Position.y + vl,
				Position.z - vl,
				Position.z + vl
			);
		}

		public float CollisionRadiusSqr {
			get {
				var v1 = math.length(Velocity) + Radius + 0.05f;
				return v1 * v1;
			}
		}

		public float Inertia => 2.0f / 5.0f * Radius * Radius * Mass;
		public float InvMass => 1f / Mass;

		public void ApplySurfaceImpulse(in float3 rotI, in float3 impulse)
		{
			Velocity += impulse / Mass;
			AngularMomentum += rotI;
		}

		public static float3 SurfaceVelocity(in BallData ball, in float3 surfP)
		{
			// linear velocity plus tangential velocity due to rotation
			return ball.Velocity + math.cross(ball.AngularVelocity, surfP);
		}

		public static float3 SurfaceAcceleration(in BallData ball, in float3 surfP, in float3 gravity)
		{
			// if we had any external torque, we would have to add "(deriv. of ang.Vel.) x surfP" here
			return gravity / ball.Mass // linear acceleration
			       + math.cross(ball.AngularVelocity, math.cross(ball.AngularVelocity, surfP)); // centripetal acceleration
		}

		public static void SetOutsideOf(ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, ref Entity entity)
		{
			int i;
			var inside = false;
			for (i = 0; i < insideOfs.Length; i++) {
				if (insideOfs[i].Value == entity) {
					inside = true;
					break;
				}
			}
			if (inside) {
				insideOfs.RemoveAt(i);
			}
		}

		public static void SetInsideOf(ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, ref Entity entity)
		{
			insideOfs.Add(new BallInsideOfBufferElement {Value = entity});
		}

		public static bool IsOutsideOf(ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, ref Entity entity)
		{
			return !IsInsideOf(ref insideOfs, ref entity);
		}

		public static bool IsInsideOf(ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, ref Entity entity)
		{
			for (var i = 0; i < insideOfs.Length; i++) {
				if (insideOfs[i].Value == entity) {
					return true;
				}
			}
			return false;
		}

		public override string ToString()
		{
			return $"Ball{Id} ({Position.x}/{Position.y})";
		}
	}
}
