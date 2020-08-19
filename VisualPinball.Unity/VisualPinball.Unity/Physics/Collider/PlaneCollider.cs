using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Unity
{
	public struct PlaneCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private float3 _normal;
		private float _distance;

		public ColliderType Type => _header.Type;

		public static void Create(BlobBuilder builder, HitPlane src, ref BlobPtr<Collider> dest)
		{
			ref var ptr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<PlaneCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref ptr);
			collider.Init(src);
		}

		private void Init(HitPlane src)
		{
			_header.Init(ColliderType.Plane, src);

			_normal = src.Normal.ToUnityFloat3();
			_distance = src.D;
		}

		public override string ToString()
		{
			return $"PlaneCollider[{_header.Entity}] {_distance} at ({_normal.x}/{_normal.y}/{_normal.z})";
		}

		public float HitTest(ref CollisionEventData collEvent, in BallData ball, float dTime)
		{
			// speed in normal direction
			var bnv = math.dot(_normal, ball.Velocity);

			// return if clearly ball is receding from object
			if (bnv > PhysicsConstants.ContactVel) {
				return -1.0f;
			}

			// distance from plane to ball surface
			var bnd = math.dot(_normal, ball.Position) - ball.Radius - _distance;

			//!! solely responsible for ball through playfield?? check other places, too (radius*2??)
			if (bnd < ball.Radius * -2.0) {
				// excessive penetration of plane ... no collision HACK
				return -1.0f;
			}

			if (math.abs(bnv) <= PhysicsConstants.ContactVel) {
				if (math.abs(bnd) <= PhysicsConstants.PhysTouch) {
					collEvent.IsContact = true;
					collEvent.HitNormal = _normal;
					collEvent.HitOrgNormalVelocity = bnv; // remember original normal velocity
					collEvent.HitDistance = bnd;

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

			collEvent.HitNormal = _normal;
			collEvent.HitDistance = bnd; // actual contact distance

			return hitTime;
		}

		public void Collide(ref BallData ball, in CollisionEventData collEvent, ref Random random)
		{
			BallCollider.Collide3DWall(ref ball, in _header.Material, in collEvent, in collEvent.HitNormal, ref random);

			// distance from plane to ball surface
			var bnd = math.dot(_normal, ball.Position) - ball.Radius - _distance;
			if (bnd < 0) {
				// if ball has penetrated, push it out of the plane
				ball.Position += _normal * bnd;
			}
		}
	}
}
