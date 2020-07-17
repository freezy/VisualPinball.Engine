// ReSharper disable CommentTypo

using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class LineSeg : HitObject
	{
		public readonly Vertex2D V1;
		public readonly Vertex2D V2;
		public readonly Vertex2D Normal = new Vertex2D();
		public float Length;

		public LineSeg(Vertex2D p1, Vertex2D p2, float zLow, float zHigh, ItemType objType) : base(objType)
		{
			V1 = p1;
			V2 = p2;
			HitBBox.ZLow = zLow;
			HitBBox.ZHigh = zHigh;
			CalcNormal();
			CalcHitBBox();
		}

		public LineSeg SetSeg(float x1, float y1, float x2, float y2)
		{
			V1.X = x1;
			V1.Y = y1;
			V2.X = x2;
			V2.Y = y2;
			CalcNormal().CalcHitBBox();
			return this;
		}

		public override void CalcHitBBox()
		{
			// Allow roundoff
			HitBBox.Left = MathF.Min(V1.X, V2.X);
			HitBBox.Right = MathF.Max(V1.X, V2.X);
			HitBBox.Top = MathF.Min(V1.Y, V2.Y);
			HitBBox.Bottom = MathF.Max(V1.Y, V2.Y);

			// zlow and zhigh were already set in constructor
		}

		public override float HitTest(Ball ball, float dTime, CollisionEvent coll, PlayerPhysics physics)
		{
			return HitTestBasic(ball, dTime, coll, true, true, true); // normal face, lateral, rigid
		}

		public float HitTestBasic(Ball ball, float dTime, CollisionEvent coll, bool direction, bool lateral, bool rigid)
		{
			if (!IsEnabled || ball.State.IsFrozen) {
				return -1.0f;
			}

			// ball velocity
			var ballVx = ball.Hit.Vel.X;
			var ballVy = ball.Hit.Vel.Y;

			// ball velocity normal to segment, positive if receding, zero=parallel
			var bnv = ballVx * Normal.X + ballVy * Normal.Y;
			var isUnHit = bnv > PhysicsConstants.LowNormVel;

			// direction true and clearly receding from normal face
			if (direction && bnv > PhysicsConstants.LowNormVel) {
				return -1.0f;
			}

			// ball position
			var ballX = ball.State.Pos.X;
			var ballY = ball.State.Pos.Y;

			// ball normal contact distance distance normal to segment. lateral contact subtract the ball radius
			var rollingRadius = lateral ? ball.Data.Radius : PhysicsConstants.ToleranceRadius; // lateral or rolling point
			var bcpd = (ballX - V1.X) * Normal.X +
			           (ballY - V1.Y) * Normal.Y; // ball center to plane distance
			var bnd = bcpd - rollingRadius;

			// for a spinner add the ball radius otherwise the ball goes half through the spinner until it moves
			if (ObjType == ItemType.Spinner || ObjType == ItemType.Gate) {
				bnd = bcpd + rollingRadius;
			}

			var inside = bnd <= 0; // in ball inside object volume
			float hitTime;
			if (rigid) {
				if (bnd < -ball.Data.Radius || lateral && bcpd < 0) {
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
					// outside-receding || inside-approaching
					if (ObjType != ItemType.Trigger // not a trigger
					    || !ball.Hit.IsRealBall() // is a trigger, so test:
					    || MathF.Abs(bnd) >= ball.Data.Radius * 0.5 // not too close ... nor too far away
					    || inside == ball.Hit.VpVolObjs.Contains(Obj)) {
						// ...Ball outside and hit set or ball inside and no hit set
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

			var btv = ballVx * Normal.Y - ballVy * Normal.X; // ball velocity tangent to segment with respect to direction from V1 to V2
			var btd = (ballX - V1.X) * Normal.Y
			          - (ballY - V1.Y) * Normal.X // ball tangent distance
			          + btv * hitTime; // ball tangent distance (projection) (initial position + velocity * hitime)

			if (btd < -PhysicsConstants.ToleranceEndPoints || btd > Length + PhysicsConstants.ToleranceEndPoints) {
				// is the contact off the line segment???
				return -1.0f;
			}

			if (!rigid) {
				// non rigid body collision? return direction
				coll.HitFlag = isUnHit; // UnHit signal is receding from outside target
			}

			var ballRadius = ball.Data.Radius;
			var hitZ = ball.State.Pos.Z +
			           ball.Hit.Vel.Z * hitTime; // check too high or low relative to ball rolling point at hittime

			if (hitZ + ballRadius * 0.5 < HitBBox.ZLow // check limits of object"s height and depth
			    || hitZ - ballRadius * 0.5 > HitBBox.ZHigh) {
				return -1.0f;
			}

			// hit normal is same as line segment normal
			coll.HitNormal.Set(Normal.X, Normal.Y, 0.0f);
			coll.HitDistance = bnd; // actual contact distance ...
			//coll.M_hitRigid = rigid;     // collision type

			// check for contact
			if (MathF.Abs(bnv) <= PhysicsConstants.ContactVel && MathF.Abs(bnd) <= PhysicsConstants.PhysTouch) {
				coll.IsContact = true;
				coll.HitOrgNormalVelocity = bnv;
			}

			return hitTime;
		}

		public override void Collide(CollisionEvent coll, PlayerPhysics physics)
		{
			var dot = coll.HitNormal.Dot(coll.Ball.Hit.Vel);
			coll.Ball.Hit.Collide3DWall(coll.HitNormal, Elasticity, ElasticityFalloff, Friction, Scatter);

			if (dot <= -Threshold) {
				FireHitEvent(coll.Ball);
			}
		}

		private LineSeg CalcNormal()
		{
			var vT = new Vertex2D(V1.X - V2.X, V1.Y - V2.Y);

			// Set up line normal
			Length = vT.Length();
			var invLength = 1.0f / Length;
			Normal.Set(vT.Y * invLength, -vT.X * invLength);
			return this;
		}
	}
}
