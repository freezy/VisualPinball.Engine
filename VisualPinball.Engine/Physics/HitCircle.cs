using System;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class HitCircle : HitObject
	{
		public readonly Vertex2D Center;
		public readonly float Radius;

		public HitCircle(Vertex2D center, float radius, float zLow, float zHigh, ItemType itemType) : base(itemType)
		{
			Center = center;
			Radius = radius;
			HitBBox.ZLow = zLow;
			HitBBox.ZHigh = zHigh;
		}

		public override void Collide(CollisionEvent coll, PlayerPhysics physics)
		{
			coll.Ball.Hit.Collide3DWall(coll.HitNormal, Elasticity, ElasticityFalloff, Friction,
				Scatter);
		}

		public override void CalcHitBBox()
		{
			// Allow roundoff
			HitBBox.Left = Center.X - Radius;
			HitBBox.Right = Center.X + Radius;
			HitBBox.Top = Center.Y - Radius;
			HitBBox.Bottom = Center.Y + Radius;
			// zlow & zhigh already set in ctor
		}

		public override float HitTest(Ball ball, float dTime, CollisionEvent coll, PlayerPhysics physics)
		{
			// normal face, lateral, rigid
			return HitTestBasicRadius(ball, dTime, coll, true, true, true);
		}

		protected float HitTestBasicRadius(Ball ball, float dTime, CollisionEvent coll, bool direction, bool lateral, bool rigid)
		{
			if (!IsEnabled || ball.State.IsFrozen) {
				return -1.0f;
			}

			var c = new Vertex3D(Center.X, Center.Y, 0.0f);
			var dist = ball.State.Pos.Clone().Sub(c); // relative ball position
			var dv = ball.Hit.Vel.Clone();

			var capsule3D = !lateral && ball.State.Pos.Z > HitBBox.ZHigh;
			var isKicker = ObjType == ItemType.Kicker;
			var isKickerOrTrigger = ObjType == ItemType.Trigger || ObjType == ItemType.Kicker;

			float targetRadius;
			if (capsule3D) {
				targetRadius = Radius * (float) (13.0 / 5.0);
				c.Z = HitBBox.ZHigh - Radius * (float) (12.0 / 5.0);
				dist.Z = ball.State.Pos.Z - c.Z; // ball rolling point - capsule center height
			}
			else {
				targetRadius = Radius;
				if (lateral) {
					targetRadius += ball.Data.Radius;
				}

				dist.Z = 0.0f;
				dv.Z = 0.0f;
			}

			var bcddsq = dist.LengthSq();        // ball center to circle center distance ... squared
			var bcdd = MathF.Sqrt(bcddsq);       // distance center to center
			if (bcdd <= 1.0e-6) {
				// no hit on exact center
				return -1.0f;
			}

			var b = dist.Dot(dv);
			var bnv = b / bcdd;                  // ball normal velocity

			if (direction && bnv > PhysicsConstants.LowNormVel) {
				// clearly receding from radius
				return -1.0f;
			}

			var bnd = bcdd - targetRadius;       // ball normal distance to

			var a = dv.LengthSq();

			var hitTime = 0f;
			var isUnhit = false;
			var isContact = false;

			// Kicker is special.. handle ball stalled on kicker, commonly hit while receding, knocking back into kicker pocket
			if (isKicker && bnd <= 0 && bnd >= -Radius &&
			    a < PhysicsConstants.ContactVel * PhysicsConstants.ContactVel && ball.Hit.IsRealBall()) {
				if (ball.Hit.VpVolObjs.Contains(Obj)) {
					ball.Hit.VpVolObjs.Remove(Obj); // cause capture
				}
			}

			// contact positive possible in future ... objects Negative in contact now
			if (rigid && bnd < PhysicsConstants.PhysTouch) {
				if (bnd < -ball.Data.Radius) {
					return -1.0f;
				}

				if (MathF.Abs(bnv) <= PhysicsConstants.ContactVel) {
					isContact = true;

				} else {
					// estimate based on distance and speed along distance
					// the ball can be that fast that in the next hit cycle the ball will be inside the hit shape of a bumper or other element.
					// if that happens bnd is negative and greater than the negative bnv value that results in a negative hittime
					// below the "if (infNan(hittime) || hittime <0.F...)" will then be true and the hit function will return -1.0f = no hit
					hitTime = MathF.Max(0.0f, (float) (-bnd / bnv));
				}

			} else if (isKickerOrTrigger && ball.Hit.IsRealBall() && bnd < 0 == ball.Hit.VpVolObjs.IndexOf(Obj) < 0) {
				// triggers & kickers

				// here if ... ball inside and no hit set .... or ... ball outside and hit set
				if (MathF.Abs(bnd - Radius) < 0.05) {
					// if ball appears in center of trigger, then assumed it was gen"ed there
					ball.Hit.VpVolObjs.Add(Obj); // special case for trigger overlaying a kicker

				} else {
					// this will add the ball to the trigger space without a Hit
					isUnhit = bnd > 0; // ball on outside is UnHit, otherwise it"s a Hit
				}

			} else {
				if (!rigid && bnd * bnv > 0 || a < 1.0e-8) {
					// (outside and receding) or (inside and approaching)
					// no hit ... ball not moving relative to object
					return -1.0f;
				}

				var sol = Functions.SolveQuadraticEq(a, 2.0f * b, bcddsq - targetRadius * targetRadius);
				if (sol == null) {
					return -1.0f;
				}

				var (time1, time2) = sol;
				isUnhit = time1 * time2 < 0;
				hitTime = isUnhit ? MathF.Max(time1, time2) : MathF.Min(time1, time2); // ball is inside the circle
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				// contact out of physics frame
				return -1.0f;
			}

			var hitZ = ball.State.Pos.Z + ball.Hit.Vel.Z * hitTime; // rolling point
			if (hitZ + ball.Data.Radius * 0.5 < HitBBox.ZLow
			    || !capsule3D && hitZ - ball.Data.Radius * 0.5 > HitBBox.ZHigh
			    || capsule3D && hitZ < HitBBox.ZHigh) {
				return -1.0f;
			}

			var hitX = ball.State.Pos.X + ball.Hit.Vel.X * hitTime;
			var hitY = ball.State.Pos.Y + ball.Hit.Vel.Y * hitTime;
			var sqrLen = (hitX - c.X) * (hitX - c.X) + (hitY - c.Y) * (hitY - c.Y);

			coll.HitNormal.SetZero();

			// over center?
			if (sqrLen > 1.0e-8) {
				// no
				var invLen = 1.0f / MathF.Sqrt(sqrLen);
				coll.HitNormal.X = (hitX - c.X) * invLen;
				coll.HitNormal.Y = (hitY - c.Y) * invLen;

			} else {
				// yes, over center
				coll.HitNormal.X = 0.0f; // make up a value, any direction is ok
				coll.HitNormal.Y = 1.0f;
				coll.HitNormal.Z = 0.0f;
			}

			if (!rigid) {
				// non rigid body collision? return direction
				coll.HitFlag = isUnhit; // UnHit signal is receding from target
			}

			coll.IsContact = isContact;
			if (isContact) {
				coll.HitOrgNormalVelocity = bnv;
			}

			coll.HitDistance = bnd; // actual contact distance ...
			//coll.M_hitRigid = rigid;                         // collision type

			return hitTime;
		}
	}
}
