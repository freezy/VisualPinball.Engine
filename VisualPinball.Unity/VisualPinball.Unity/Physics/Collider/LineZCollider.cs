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
	public struct LineZCollider
	{
		private ColliderHeader _header;

		private float2 _xy;
		private float _zLow;
		private float _zHigh;

		public static void Create(BlobBuilder builder, HitLineZ src, ref BlobPtr<Collider> dest)
		{
			ref var linePtr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<LineZCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref linePtr);
			collider.Init(src);
		}

		private void Init(HitLineZ src)
		{
			_header.Init(ColliderType.LineZ, src);

			_xy = src.Xy.ToUnityFloat2();
			_zLow = src.HitBBox.ZLow;
			_zHigh = src.HitBBox.ZHigh;
		}

		public float HitTest(ref CollisionEventData collEvent, in BallData ball, float dTime)
		{
			return HitTest(ref collEvent, in this, in ball, dTime);
		}


		public static float HitTest(ref CollisionEventData collEvent, in LineZCollider coll, in BallData ball, float dTime)
		{
			// todo
			// if (!IsEnabled) {
			// 	return -1.0f;
			// }

			var bp2d = new float2(ball.Position.x, ball.Position.y);
			var dist = bp2d - coll._xy;                                       // relative ball position
			var dv = new float2(ball.Velocity.x, ball.Velocity.y);

			var bcddsq = math.lengthsq(dist);                             // ball center to line distance squared
			var bcdd = math.sqrt(bcddsq);                                     // distance ball to line
			if (bcdd <= 1.0e-6) {
				// no hit on exact center
				return -1.0f;
			}

			var b = math.dot(dist, dv);
			var bnv = b / bcdd;                                                // ball normal velocity

			if (bnv > PhysicsConstants.ContactVel) {
				// clearly receding from radius
				return -1.0f;
			}

			var bnd = bcdd - ball.Radius;                                 // ball distance to line
			var a = math.lengthsq(dv);

			float hitTime;
			var isContact = false;

			if (bnd < PhysicsConstants.PhysTouch) {
				// already in collision distance?
				if (math.abs(bnv) <= PhysicsConstants.ContactVel) {
					isContact = true;
					hitTime = 0f;

				} else {
					// estimate based on distance and speed along distance
					hitTime = -bnd / bnv;
				}

			} else {
				if (a < 1.0e-8) {
					// no hit - ball not moving relative to object
					return -1.0f;
				}

				var sol = Functions.SolveQuadraticEq(a, 2.0f * b, bcddsq - ball.Radius * ball.Radius);
				if (sol == null) {
					return -1.0f;
				}

				var time1 = sol.Item1;
				var time2 = sol.Item2;

				// find smallest non-negative solution
				hitTime = time1 * time2 < 0 ? math.max(time1, time2) : math.min(time1, time2);
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				// contact out of physics frame
				return -1.0f;
			}

			var hitZ = ball.Position.z + hitTime * ball.Velocity.z;            // ball z position at hit time

			if (hitZ < coll._zLow || hitZ > coll._zHigh) {
				// check z coordinate
				return -1.0f;
			}

			var hitX = ball.Position.x + hitTime * ball.Velocity.x;            // ball x position at hit time
			var hitY = ball.Position.y + hitTime * ball.Velocity.y;            // ball y position at hit time

			var norm = math.normalize(new float2(hitX - coll._xy.x, hitY - coll._xy.y));
			collEvent.HitNormal.x = norm.x;
			collEvent.HitNormal.y = norm.y;
			collEvent.HitNormal.z = 0f;

			collEvent.IsContact = isContact;
			if (isContact) {
				collEvent.HitOrgNormalVelocity = bnv;
			}

			collEvent.HitDistance = bnd; // actual contact distance
			//coll.M_hitRigid = true;

			return hitTime;
		}
	}
}
