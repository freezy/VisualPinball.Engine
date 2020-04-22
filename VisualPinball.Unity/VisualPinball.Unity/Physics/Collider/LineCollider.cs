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

		public static void Create(BlobBuilder builder, LineSeg src, ref BlobPtr<Collider> dest)
		{
			ref var linePtr = ref UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<LineCollider>>(ref dest);
			ref var collider = ref builder.Allocate(ref linePtr);
			collider.Init(src);
		}

		private void Init(LineSeg src)
		{
			_header.Type = ColliderType.Line;
			_header.ItemType = Collider.GetItemType(src.ObjType);
			_header.Id = src.Id;
			_header.Entity = new Entity {Index = src.ItemIndex, Version = src.ItemVersion};

			_v1 = src.V1.ToUnityFloat2();
			_v2 = src.V2.ToUnityFloat2();
			_zHigh = src.HitBBox.ZHigh;
			_zLow = src.HitBBox.ZLow;

			CalcNormal();
		}

		private void CalcNormal()
		{
			var vT = new float2(_v1.x - _v2.x, _v1.y - _v2.y);

			// Set up line normal
			_length = math.length(vT);
			var invLength = 1.0f / _length;
			_normal.x = vT.y * invLength;
			_normal.y = -vT.x * invLength;
		}

		public float HitTest(ref CollisionEventData coll, in BallData ball, float dTime)
		{
			return HitTestBasic(ref coll, ball, dTime, true, true, true); // normal face, lateral, rigid
		}

		public float HitTestBasic(ref CollisionEventData coll, in BallData ball, float dTime, bool direction, bool lateral, bool rigid)
		{
			// if (!IsEnabled || ball.State.IsFrozen) {
			// 	return -1.0f;
			// }

			// ball velocity
			var ballVx = ball.Velocity.x;
			var ballVy = ball.Velocity.y;

			// ball velocity normal to segment, positive if receding, zero=parallel
			var bnv = ballVx * _normal.x + ballVy * _normal.y;
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
			var bcpd = (ballX - _v1.x) * _normal.x + (ballY - _v1.y) * _normal.y; // ball center to plane distance
			var bnd = bcpd - rollingRadius;

			// todo for a spinner add the ball radius otherwise the ball goes half through the spinner until it moves
			// if (ObjType == CollisionType.Spinner || ObjType == CollisionType.Gate) {
			// 	bnd = bcpd + rollingRadius;
			// }

			var inside = bnd <= 0; // in ball inside object volume
			float hitTime;
			if (rigid) {
				if (bnd < -ball.Radius || lateral && bcpd < 0) {
					// (ball normal distance) excessive penetration of object skin ... no collision HACK
					return -1.0f;
				}

				if (lateral && bnd <= PhysicsConstants.PhysTouch) {
					if (inside
					    || MathF.Abs(bnv) > PhysicsConstants.ContactVel // fast velocity, return zero time
					    || bnd <= -PhysicsConstants.PhysTouch) {
						// zero time for rigid fast bodies
						hitTime = 0; // slow moving but embedded

					} else {
						hitTime = bnd * (float)(1.0 / (2.0 * PhysicsConstants.PhysTouch)) + 0.5f; // don't compete for fast zero time events
					}

				} else if (MathF.Abs(bnv) > PhysicsConstants.LowNormVel) {
					// not velocity low ????
					hitTime = bnd / -bnv; // rate ok for safe divide
				} else {
					return -1.0f; // wait for touching
				}

			} else {
				//non-rigid ... target hits
				if (bnv * bnd >= 0) {
					// todo outside-receding || inside-approaching
					// if (ObjType != CollisionType.Trigger // not a trigger
					//     || !ball.Hit.IsRealBall() // is a trigger, so test:
					//     || MathF.Abs(bnd) >= ball.Radius * 0.5 // not too close ... nor too far away
					//     || inside == ball.Hit.VpVolObjs.Contains(Obj)) {
					// 	// ...Ball outside and hit set or ball inside and no hit set
					// 	return -1.0f;
					// }

					hitTime = 0;
					isUnHit = !inside; // ball on outside is UnHit, otherwise it"s a Hit

				} else {
					hitTime = bnd / -bnv;
				}
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				return -1.0f; // time is outside this frame ... no collision
			}

			var btv = ballVx * _normal.y - ballVy * _normal.x; // ball velocity tangent to segment with respect to direction from _v1 to _v2
			var btd = (ballX - _v1.x) * _normal.y
			             - (ballY - _v1.y) * _normal.x    // ball tangent distance
			             + btv * hitTime;                 // ball tangent distance (projection) (initial position + velocity * hitime)

			if (btd < -PhysicsConstants.ToleranceEndPoints || btd > _length + PhysicsConstants.ToleranceEndPoints) {
				// is the contact off the line segment???
				return -1.0f;
			}

			if (!rigid) {
				// todo non rigid body collision? return direction
				//coll.HitFlag = isUnHit; // UnHit signal is receding from outside target
			}

			var ballRadius = ball.Radius;
			var hitZ = ball.Position.z + ball.Velocity.z * hitTime; // check too high or low relative to ball rolling point at hittime

			if (hitZ + ballRadius * 0.5 < _zLow // check limits of object"s height and depth
			    || hitZ - ballRadius * 0.5 > _zHigh) {
				return -1.0f;
			}

			// hit normal is same as line segment normal
			coll.HitNormal.x = _normal.x;
			coll.HitNormal.y = _normal.y;
			coll.HitNormal.z = 0f;
			coll.HitDistance = bnd; // actual contact distance ...
			//coll.M_hitRigid = rigid;     // collision type

			// check for contact
			if (MathF.Abs(bnv) <= PhysicsConstants.ContactVel && MathF.Abs(bnd) <= PhysicsConstants.PhysTouch) {
				coll.IsContact = true;
				coll.HitOrgNormalVelocity = bnv;
			}
			return hitTime;
		}

		public void Collide(ref BallData ball, in CollisionEventData coll)
		{
			BallCollider.Collide3DWall(ref ball, in _header.Material, in coll, in coll.HitNormal);

			// todo
			// var dot = math.dot(coll.HitNormal, ball.Velocity);
			// if (dot <= -Threshold) {
			// 	FireHitEvent(coll.Ball);
			// }
		}
	}
}
