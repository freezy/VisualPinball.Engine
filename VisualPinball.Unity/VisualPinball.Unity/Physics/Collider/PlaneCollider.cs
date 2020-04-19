using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct PlaneCollider : ICollider, ICollidable
	{
		public ColliderHeader Header;

		public float3 Normal;
		public float Distance;

		public ColliderType Type => Header.Type;

		public static void Create(BlobBuilder builder, HitPlane src, ref BlobPtr<Collider> dest)
		{
			ref var ptr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<PlaneCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref ptr);
			collider.Init(src);
		}

		private void Init(HitPlane src)
		{
			Header.Type = ColliderType.Plane;
			Header.EntityIndex = src.ItemIndex;
			Header.Aabb = src.HitBBox.ToAabb();
			Header.Material = new PhysicsMaterialData {
				Elasticity = src.Elasticity,
				ElasticityFalloff = src.ElasticityFalloff,
				Friction = src.Friction,
				Scatter = src.Scatter,
			};

			Normal = src.Normal.ToUnityFloat3();
			Distance = src.D;
		}

		public override string ToString()
		{
			return $"PlaneCollider[{Header.EntityIndex}] {Distance} at ({Normal.x}/{Normal.y}/{Normal.z})";
		}

		public float HitTest(ref CollisionEventData coll, in BallData ball, float dTime)
		{
			// speed in normal direction
			var bnv = math.dot(Normal, ball.Velocity);

			// return if clearly ball is receding from object
			if (bnv > PhysicsConstants.ContactVel) {
				return -1.0f;
			}

			// distance from plane to ball surface
			var bnd = math.dot(Normal, ball.Position) - ball.Radius - Distance;

			//!! solely responsible for ball through playfield?? check other places, too (radius*2??)
			if (bnd < ball.Radius * -2.0) {
				// excessive penetration of plane ... no collision HACK
				return -1.0f;
			}

			if (math.abs(bnv) <= PhysicsConstants.ContactVel) {
				if (math.abs(bnd) <= PhysicsConstants.PhysTouch) {
					coll.IsContact = true;
					coll.HitNormal = Normal;
					coll.HitOrgNormalVelocity = bnv; // remember original normal velocity
					coll.HitDistance = bnd;

					// hit time is ignored for contacts
					return 0.0f;
				}

				// large distance, small velocity -> no hit
				return -1.0f;
			}

			var hitTime = bnd / -bnv;

			// already penetrating? then collide immediately
			if (hitTime < 0) {
				hitTime = 0.0f;
			}

			// time is outside this frame ... no collision
			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				return -1.0f;
			}

			coll.HitNormal = Normal;
			coll.HitDistance = bnd; // actual contact distance

			return hitTime;
		}

		public void Collide(ref BallData ball, CollisionEventData coll)
		{
			BallCollider.Collide3DWall(ref ball, ref Header.Material, ref coll, coll.HitNormal);

			// distance from plane to ball surface
			var bnd = math.dot(Normal, ball.Position) - ball.Radius - Distance;
			if (bnd < 0) {
				// if ball has penetrated, push it out of the plane
				ball.Position += Normal * bnd;
			}
		}
	}
}
