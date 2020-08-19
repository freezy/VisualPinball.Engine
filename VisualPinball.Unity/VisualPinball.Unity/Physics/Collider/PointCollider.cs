using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Unity
{
	public struct PointCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private float3 _p;

		public ColliderType Type => _header.Type;

		public static void Create(BlobBuilder builder, HitPoint src, ref BlobPtr<Collider> dest)
		{
			ref var colliderPtr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<PointCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref colliderPtr);
			collider.Init(src);
		}

		private void Init(HitPoint src)
		{
			_header.Init(ColliderType.Point, src);
			_p = src.P.ToUnityFloat3();
		}

		#region Narrowphase

		public float HitTest(ref CollisionEventData collEvent, in BallData ball, float dTime)
		{
			// todo
			// if (!IsEnabled) {
			// 	return -1.0f;
			// }

			// relative ball position
			var dist = ball.Position - _p;

			var bcddsq = math.lengthsq(dist);                                  // ball center to line distance squared
			var bcdd = math.sqrt(bcddsq);                                      // distance ball to line
			if (bcdd <= 1.0e-6) {
				// no hit on exact center
				return -1.0f;
			}

			var b = math.dot(dist, ball.Velocity);
			var bnv = b / bcdd;                                                // ball normal velocity

			if (bnv > PhysicsConstants.ContactVel) {
				// clearly receding from radius
				return -1.0f;
			}

			var bnd = bcdd - ball.Radius;                                      // ball distance to line
			var a = math.lengthsq(ball.Velocity);

			float hitTime;
			var isContact = false;

			if (bnd < PhysicsConstants.PhysTouch) {
				// already in collision distance?
				if (math.abs(bnv) <= PhysicsConstants.ContactVel) {
					isContact = true;
					hitTime = 0;

				} else {
					// estimate based on distance and speed along distance
					hitTime = math.max(0.0f, -bnd / bnv);
				}

			} else {
				if (a < 1.0e-8) {
					// no hit - ball not moving relative to object
					return -1.0f;
				}

				var solved = Math.SolveQuadraticEq(a, 2.0f * b, bcddsq - ball.Radius * ball.Radius,
					out var time1, out var time2);
				if (!solved) {
					return -1.0f;
				}

				// find smallest non-negative solution
				hitTime = time1 * time2 < 0 ? math.max(time1, time2) : math.min(time1, time2);
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				// contact out of physics frame
				return -1.0f;
			}

			var hitPos = ball.Position + hitTime * ball.Velocity;
			collEvent.HitNormal = math.normalize(hitPos - _p);

			collEvent.IsContact = isContact;
			if (isContact) {
				collEvent.HitOrgNormalVelocity = bnv;
			}

			collEvent.HitDistance = bnd; // actual contact distance
			//coll.M_hitRigid = true;

			return hitTime;
		}

		#endregion

		public void Collide(ref BallData ball,  ref NativeQueue<EventData>.ParallelWriter hitEvents,
			in CollisionEventData collEvent, ref Random random)
		{
			var dot = math.dot(collEvent.HitNormal, ball.Velocity);
			BallCollider.Collide3DWall(ref ball, in _header.Material, in collEvent, in collEvent.HitNormal, ref random);

			if (dot <= -_header.Threshold) {
				Collider.FireHitEvent(ref ball, ref hitEvents, in _header);
			}
		}
	}
}
