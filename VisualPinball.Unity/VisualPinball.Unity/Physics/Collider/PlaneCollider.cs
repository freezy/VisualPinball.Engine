using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Physics.Collision;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct PlaneCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private float3 _normal;
		private float _d;

		public ColliderType Type => _header.Type;

		public static void Create(BlobBuilder builder, HitPlane src, ref BlobPtr<Collider> dest)
		{
			ref var ptr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<PlaneCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref ptr);
			collider.Init(src);
		}

		public static Collider Create(HitPlane src)
		{
			var collider = default(PlaneCollider);
			collider.Init(src);
			ref var ptr = ref UnsafeUtilityEx.As<PlaneCollider, Collider>(ref collider);
			return ptr;
		}

		private void Init(HitPlane src)
		{
			_header.Type = ColliderType.Plane;
			_header.EntityIndex = src.ItemIndex;
			_header.Aabb = src.HitBBox.ToAabb();
			_header.Material = new PhysicsMaterialData {
				Elasticity = src.Elasticity,
				ElasticityFalloff = src.ElasticityFalloff,
				Friction = src.Friction,
				Scatter = src.Scatter,
			};

			_normal = src.Normal.ToUnityFloat3();
			_d = src.D;
		}

		public float HitTest(in BallData ball, float dTime, CollisionEventData coll)
		{

			var bnv = math.dot(_normal, ball.Velocity); // speed in normal direction

			if (bnv > PhysicsConstants.ContactVel) {
				// return if clearly ball is receding from object
				return -1.0f;
			}

			var bnd = math.dot(_normal, ball.Position) - ball.Radius - _d; // distance from plane to ball surface

			//!! solely responsible for ball through playfield?? check other places, too (radius*2??)
			if (bnd < ball.Radius * -2.0) {
				// excessive penetration of plane ... no collision HACK
				return -1.0f;
			}

			if (math.abs(bnv) <= PhysicsConstants.ContactVel) {
				if (math.abs(bnd) <= PhysicsConstants.PhysTouch) {
					coll.IsContact = true;
					coll.HitNormal = _normal;
					coll.HitOrgNormalVelocity = bnv; // remember original normal velocity
					coll.HitDistance = bnd;
					return 0.0f; // hit time is ignored for contacts
				}
				return -1.0f; // large distance, small velocity -> no hit
			}

			var hitTime = bnd / (-bnv);
			if (hitTime < 0) {
				hitTime = 0.0f; // already penetrating? then collide immediately
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				// time is outside this frame ... no collision
				return -1.0f;
			}

			coll.HitNormal = _normal;
			coll.HitDistance = bnd; // actual contact distance

			return hitTime;
		}

		public void Collide(ref BallData ball, CollisionEventData coll)
		{
			BallCollider.Collide3DWall(ref ball, ref _header.Material, ref coll, coll.HitNormal);

			// distance from plane to ball surface
			var bnd = math.dot(_normal, ball.Position) - ball.Radius - _d;
			if (bnd < 0) {
				// if ball has penetrated, push it out of the plane
				ball.Position += _normal * bnd;
			}
		}
	}
}
