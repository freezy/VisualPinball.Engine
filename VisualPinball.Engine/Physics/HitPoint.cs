using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class HitPoint : HitObject
	{
		private readonly Vertex3D _p;

		public HitPoint(Vertex3D p)
		{
			_p = p;
		}

		public override void CalcHitBBox()
		{
			HitBBox = new Rect3D(_p.X, _p.X, _p.Y, _p.Y, _p.Z, _p.Z);
		}

		public override float HitTest(Ball ball, float dTime, CollisionEvent coll, PlayerPhysics physics)
		{
			if (!IsEnabled) {
				return -1.0f;
			}

			// relative ball position
			var dist = ball.State.Pos.Clone().Sub(_p);

			var bcddsq = dist.LengthSq();                                      // ball center to line distance squared
			var bcdd = MathF.Sqrt(bcddsq);                                     // distance ball to line
			if (bcdd <= 1.0e-6) {
				// no hit on exact center
				return -1.0f;
			}

			var b = dist.Dot(ball.Hit.Vel);
			var bnv = b / bcdd;                                                // ball normal velocity

			if (bnv > PhysicsConstants.ContactVel) {
				// clearly receding from radius
				return -1.0f;
			}

			var bnd = bcdd - ball.Data.Radius;                                 // ball distance to line
			var a = ball.Hit.Vel.LengthSq();

			float hitTime;
			var isContact = false;

			if (bnd < PhysicsConstants.PhysTouch) {
				// already in collision distance?
				if (MathF.Abs(bnv) <= PhysicsConstants.ContactVel) {
					isContact = true;
					hitTime = 0;

				} else {
					// estimate based on distance and speed along distance
					hitTime = MathF.Max(0.0f, -bnd / bnv);
				}

			} else {
				if (a < 1.0e-8) {
					// no hit - ball not moving relative to object
					return -1.0f;
				}

				var sol = Functions.SolveQuadraticEq(a, 2.0f * b, bcddsq - ball.Data.Radius * ball.Data.Radius);
				if (sol == null) {
					return -1.0f;
				}

				var time1 = sol.Item1;
				var time2 = sol.Item2;

				// find smallest non-negative solution
				hitTime = time1 * time2 < 0 ? MathF.Max(time1, time2) : MathF.Min(time1, time2);
			}

			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				// contact out of physics frame
				return -1.0f;
			}

			var hitVel = ball.Hit.Vel.Clone().MultiplyScalar(hitTime);
			var hitNormal = ball.State.Pos.Clone()
				.Add(hitVel)
				.Sub(_p)
				.Normalize();
			coll.HitNormal.Set(hitNormal);

			coll.IsContact = isContact;
			if (isContact) {
				coll.HitOrgNormalVelocity = bnv;
			}

			coll.HitDistance = bnd; // actual contact distance
			//coll.M_hitRigid = true;

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
	}
}
