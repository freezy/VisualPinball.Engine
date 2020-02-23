using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class HitLineZ : HitObject
	{
		protected readonly Vertex2D Xy;

		protected HitLineZ(Vertex2D xy)
		{
			Xy = xy;
		}

		public HitLineZ(Vertex2D xy, float zLow, float zHigh) : this(xy)
		{
			HitBBox.ZLow = zLow;
			HitBBox.ZHigh = zHigh;
		}

		public HitLineZ Set(float x, float y)
		{
			Xy.X = x;
			Xy.Y = y;
			return this;
		}

		public override void CalcHitBBox()
		{
			HitBBox.Left = Xy.X;
			HitBBox.Right = Xy.X;
			HitBBox.Top = Xy.Y;
			HitBBox.Bottom = Xy.Y;

			// zlow and zhigh set in ctor
		}

		public override float HitTest(Ball ball, float dTime, CollisionEvent coll, PlayerPhysics physics)
		{
			if (!IsEnabled) {
				return -1.0f;
			}

			var bp2d = new Vertex2D(ball.State.Pos.X, ball.State.Pos.Y);
			var dist = bp2d.Clone().Sub(Xy);                                   // relative ball position
			var dv = new Vertex2D(ball.Hit.Vel.X, ball.Hit.Vel.Y);

			var bcddsq = dist.LengthSq();                                      // ball center to line distance squared
			var bcdd = MathF.Sqrt(bcddsq);                                     // distance ball to line
			if (bcdd <= 1.0e-6) {
				// no hit on exact center
				return -1.0f;
			}

			var b = dist.Dot(dv);
			var bnv = b / bcdd;                                                // ball normal velocity

			if (bnv > PhysicsConstants.ContactVel) {
				// clearly receding from radius
				return -1.0f;
			}

			var bnd = bcdd - ball.Data.Radius;                                 // ball distance to line
			var a = dv.LengthSq();

			float hitTime;
			var isContact = false;

			if (bnd < PhysicsConstants.PhysTouch) {
				// already in collision distance?
				if (MathF.Abs(bnv) <= PhysicsConstants.ContactVel) {
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

			var hitZ = ball.State.Pos.Z + hitTime * ball.Hit.Vel.Z;            // ball z position at hit time

			if (hitZ < HitBBox.ZLow || hitZ > HitBBox.ZHigh) {
				// check z coordinate
				return -1.0f;
			}

			var hitX = ball.State.Pos.X + hitTime * ball.Hit.Vel.X;            // ball x position at hit time
			var hitY = ball.State.Pos.Y + hitTime * ball.Hit.Vel.Y;            // ball y position at hit time

			var norm = new Vertex2D(hitX - Xy.X, hitY - Xy.Y).Normalize();
			coll.HitNormal.Set(norm.X, norm.Y, 0.0f);

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
			coll.Ball.Hit.Collide3DWall(coll.HitNormal, Elasticity, ElasticityFalloff, Friction,
				Scatter);

			if (dot <= -Threshold) {
				FireHitEvent(coll.Ball);
			}
		}
	}
}
