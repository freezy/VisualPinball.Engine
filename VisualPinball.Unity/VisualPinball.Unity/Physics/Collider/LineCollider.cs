using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public struct LineCollider : ICollider, ICollidable
	{
		private ColliderHeader _header;

		private float2 _v1;
		private float2 _v2;
		private float2 _normal;
		private float _length;
		private float _zLow;
		private float _zHigh;

		public ColliderType Type => _header.Type;
		public ItemType ItemType => _header.ItemType;
		public Entity Entity => _header.Entity;

		public float V1y { set => _v1.y = value; }
		public float V2y { set => _v2.y = value; }

		public static void Create(BlobBuilder builder, LineSeg src, ref BlobPtr<Collider> dest, ColliderType type = ColliderType.Line)
		{
			ref var linePtr = ref UnsafeUtility.As<BlobPtr<Collider>, BlobPtr<LineCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref linePtr);
			collider.Init(src, type);
		}

		public static LineCollider Create(LineSeg src, ColliderType type = ColliderType.Line)
		{
			var collider = default(LineCollider);
			collider.Init(src, type);
			return collider;
		}

		private void Init(LineSeg src, ColliderType type)
		{
			_header.Init(type, src);

			_v1 = src.V1.ToUnityFloat2();
			_v2 = src.V2.ToUnityFloat2();
			_normal = src.Normal.ToUnityFloat2();
			_length = src.Length;
			_zLow = src.HitBBox.ZLow;
			_zHigh = src.HitBBox.ZHigh;
		}

		#region Narrowphase

		public static float HitTest(ref CollisionEventData collEvent,
			ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, in LineCollider coll, in BallData ball, float dTime)
		{
			return HitTestBasic(ref collEvent, ref insideOfs, in coll, ball, dTime, true, true, true); // normal face, lateral, rigid
		}

		public float HitTest(ref CollisionEventData collEvent, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, in BallData ball, float dTime)
		{
			return HitTestBasic(ref collEvent, ref insideOfs, in this, ball, dTime, true, true, true); // normal face, lateral, rigid
		}

		public float HitTestBasic(ref CollisionEventData collEvent, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, in BallData ball, float dTime,
			bool direction, bool lateral, bool rigid)
		{
			return HitTestBasic(ref collEvent, ref insideOfs, in this, ball, dTime, direction, lateral, rigid);
		}

		public static float HitTestBasic(ref CollisionEventData collEvent, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs, in LineCollider coll, in BallData ball, float dTime, bool direction, bool lateral, bool rigid)
		{
			// if (!IsEnabled || ball.State.IsFrozen) {
			// 	return -1.0f;
			// }

			// ball velocity
			var ballVx = ball.Velocity.x;
			var ballVy = ball.Velocity.y;

			// ball velocity normal to segment, positive if receding, zero=parallel
			var bnv = ballVx * coll._normal.x + ballVy * coll._normal.y;
			var isUnHit = bnv > PhysicsConstants.LowNormVel;

			// direction true and clearly receding from normal face
			if (direction && bnv > PhysicsConstants.LowNormVel) {
				return -1.0f;
			}

			// ball position
			var ballX = ball.Position.x;
			var ballY = ball.Position.y;

			// ball normal contact distance distance normal to segment. lateral contact subtract the ball radius
			var rollingRadius = lateral ? ball.Radius : PhysicsConstants.ToleranceRadius; // lateral or rolling point
			var bcpd = (ballX - coll._v1.x) * coll._normal.x + (ballY - coll._v1.y) * coll._normal.y; // ball center to plane distance
			var bnd = bcpd - rollingRadius;

			if (coll.ItemType == ItemType.Spinner || coll.ItemType == ItemType.Gate) {
				bnd = bcpd + rollingRadius;
			}

			var inside = bnd <= 0; // in ball inside object volume
			float hitTime;
			if (rigid) {
				if (bnd < -ball.Radius || lateral && bcpd < 0) {
					// (ball normal distance) excessive penetration of object skin ... no collision HACK
					return -1.0f;
				}

				if (lateral && bnd <= PhysicsConstants.PhysTouch) {
					if (inside
					    || math.abs(bnv) > PhysicsConstants.ContactVel // fast velocity, return zero time
					    || bnd <= -PhysicsConstants.PhysTouch) {
						// zero time for rigid fast  bodies
						hitTime = 0; // slow moving but embedded

					} else {
						hitTime = bnd * (float)(1.0 / (2.0 * PhysicsConstants.PhysTouch)) + 0.5f; // don't compete for fast zero time events
					}

				} else if (math.abs(bnv) > PhysicsConstants.LowNormVel) {
					// not velocity low ????
					hitTime = bnd / -bnv; // rate ok for safe divide
				} else {
					return -1.0f; // wait for touching
				}

			} else {
				//non-rigid ... target hits
				if (bnv * bnd >= 0) {

					if (coll.ItemType != ItemType.Trigger               // not a trigger
					    /*todo   || !ball.m_vpVolObjs*/
					    // is a trigger, so test:
					    || math.abs(bnd) >= ball.Radius * 0.5f          // not too close ... nor too far away
					    || inside == BallData.IsInsideOf(in insideOfs, coll.Entity))   // ...ball outside and hit set or ball inside and no hit set
					{
						return -1.0f;
					}

					hitTime = 0;
					isUnHit = !inside; // ball on outside is UnHit, otherwise it"s a Hit

				} else {
					hitTime = bnd / -bnv;
				}
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				return -1.0f; // time is outside this frame ... no collision
			}

			var btv = ballVx * coll._normal.y - ballVy * coll._normal.x; // ball velocity tangent to segment with respect to direction from _v1 to _v2
			var btd = (ballX - coll._v1.x) * coll._normal.y
			             - (ballY - coll._v1.y) * coll._normal.x    // ball tangent distance
			             + btv * hitTime;                 // ball tangent distance (projection) (initial position + velocity * hitime)

			if (btd < -PhysicsConstants.ToleranceEndPoints || btd > coll._length + PhysicsConstants.ToleranceEndPoints) {
				// is the contact off the line segment???
				return -1.0f;
			}

			if (!rigid) {
				collEvent.HitFlag = isUnHit; // UnHit signal is receding from outside target
			}

			var ballRadius = ball.Radius;
			var hitZ = ball.Position.z + ball.Velocity.z * hitTime; // check too high or low relative to ball rolling point at hittime

			if (hitZ + ballRadius * 0.5 < coll._zLow // check limits of object"s height and depth
			    || hitZ - ballRadius * 0.5 > coll._zHigh) {
				return -1.0f;
			}

			// hit normal is same as line segment normal
			collEvent.HitNormal.x = coll._normal.x;
			collEvent.HitNormal.y = coll._normal.y;
			collEvent.HitNormal.z = 0f;
			collEvent.HitDistance = bnd; // actual contact distance ...

			// check for contact
			collEvent.IsContact = math.abs(bnv) <= PhysicsConstants.ContactVel &&
			                      math.abs(bnd) <= PhysicsConstants.PhysTouch;
			if (collEvent.IsContact) {
				collEvent.HitOrgNormalVelocity = bnv;
			}
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

		public void CalcNormal()
		{
			var vT = new float2(_v1.x - _v2.x, _v1.y - _v2.y);

			// Set up line normal
			var invLength = 1.0f /  math.length(vT);
			_normal.x = vT.y * invLength;
			_normal.y = -vT.x * invLength;
		}
	}
}
