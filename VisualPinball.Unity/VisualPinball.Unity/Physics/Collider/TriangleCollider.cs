using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Unity
{
	public struct TriangleCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private float3 _rgv0;
		private float3 _rgv1;
		private float3 _rgv2;
		private float3 _normal;

		public ColliderType Type => _header.Type;

		public float3 Normal() => _normal;

		public static void Create(BlobBuilder builder, HitTriangle src, ref BlobPtr<Collider> dest)
		{
			ref var trianglePtr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<TriangleCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref trianglePtr);
			collider.Init(src);
		}

		private void Init(HitTriangle src)
		{
			_header.Init(ColliderType.Triangle, src);

			_rgv0 = src.Rgv[0].ToUnityFloat3();
			_rgv1 = src.Rgv[1].ToUnityFloat3();
			_rgv2 = src.Rgv[2].ToUnityFloat3();
			_normal = src.Normal.ToUnityFloat3();
		}

		#region Narrowphase

		public float HitTest(ref CollisionEventData collEvent, in BallData ball, float dTime)
		{
			// if (!this.isEnabled) {
			// 	return -1.0;
			// }

			var bnv = math.dot(_normal, ball.Velocity);         // speed in Normal-vector direction
			if (bnv > PhysicsConstants.ContactVel) {                          // return if clearly ball is receding from object
				return -1.0f;
			}

			// Point on the ball that will hit the polygon, if it hits at all
			var normRadius = ball.Radius * _normal;
			var hitPos = ball.Position - normRadius;     // nearest point on ball ... projected radius along norm
			var hpSubRgv0 = hitPos - _rgv0;
			var bnd = math.dot(_normal, hpSubRgv0);                                // distance from plane to ball

			if (bnd < -ball.Radius) {
				// (ball normal distance) excessive penetration of object skin ... no collision HACK
				return -1.0f;
			}

			var isContact = false;
			float hitTime;

			if (bnd <= PhysicsConstants.PhysTouch) {
				if (math.abs(bnv) <= PhysicsConstants.ContactVel) {
					hitTime = 0;
					isContact = true;

				} else if (bnd <= 0) {
					hitTime = 0;                               // zero time for rigid fast bodies

				} else {
					hitTime = bnd / -bnv;
				}

			} else if (math.abs(bnv) > PhysicsConstants.LowNormVel) {         // not velocity low?
				hitTime = bnd / -bnv;                          // rate ok for safe divide

			} else {
				return -1.0f;                // wait for touching
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				return -1.0f;                // time is outside this frame ... no collision
			}

			// advance hit point to contact
			var adv = hitTime * ball.Velocity;
			hitPos += adv;

			// Check if hitPos is within the triangle
			// 1. Compute vectors
			var v0 = _rgv2 - _rgv0;
			var v1 = _rgv1 - _rgv0;
			var v2 = hitPos - _rgv0;

			// 2. Compute dot products
			var dot00 = math.dot(v0, v0);
			var dot01 = math.dot(v0, v1);
			var dot02 = math.dot(v0, v2);
			var dot11 = math.dot(v1, v1);
			var dot12 = math.dot(v1, v2);

			// 3. Compute barycentric coordinates
			var invDenom = 1.0 / (dot00 * dot11 - dot01 * dot01);
			var u = (dot11 * dot02 - dot01 * dot12) * invDenom;
			var v = (dot00 * dot12 - dot01 * dot02) * invDenom;

			// 4. Check if point is in triangle
			var pointInTriangle = (u >= 0) && (v >= 0) && (u + v <= 1);

			if (pointInTriangle) {
				collEvent.HitNormal = _normal;
				collEvent.HitDistance = bnd;                        // 3dhit actual contact distance ...
				//coll.m_hitRigid = true;                      // collision type

				if (isContact) {
					collEvent.IsContact = true;
					collEvent.HitOrgNormalVelocity = bnv;
				}
				return hitTime;
			}

			return -1.0f;
		}

		#endregion

		// todo identical with Poly3DCollider.Collider, refactor?
		public void Collide(ref BallData ball,  ref NativeQueue<EventData>.ParallelWriter hitEvents,
			in CollisionEventData collEvent, ref Random random)
		{
			var dot = -math.dot(collEvent.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in _header.Material, in collEvent, in _normal, ref random);

			if (_header.FireEvents && dot >= _header.Threshold && _header.IsPrimitive) {
				// todo m_obj->m_currentHitThreshold = dot;
				Collider.FireHitEvent(ref ball, ref hitEvents, in _header);
			}
		}
	}
}
