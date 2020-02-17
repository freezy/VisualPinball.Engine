using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;

namespace VisualPinball.Engine.Physics
{
	public class HitPlane : HitObject
	{
		private readonly Vertex3D _normal;
		private readonly float _d;

		public HitPlane(Vertex3D normal, float d)
		{
			_normal = normal;
			_d = d;
		}

		public override void CalcHitBBox()
		{
			// plane"s not a box (i assume)
		}

		public override float HitTest(Ball ball, float dTime, CollisionEvent coll, PlayerPhysics physics)
		{
			if (!IsEnabled) {
				return -1.0f;
			}

			var bnv = _normal.Dot(ball.Hit.Vel); // speed in normal direction

			if (bnv > PhysicsConstants.ContactVel) {
				// return if clearly ball is receding from object
				return -1.0f;
			}

			var bnd = _normal.Dot(ball.State.Pos) - ball.Data.Radius - _d; // distance from plane to ball surface

			//!! solely responsible for ball through playfield?? check other places, too (radius*2??)
			if (bnd < ball.Data.Radius * -2.0) {
				// excessive penetration of plane ... no collision HACK
				return -1.0f;
			}

			if (MathF.Abs(bnv) <= PhysicsConstants.ContactVel) {
				if (MathF.Abs(bnd) <= PhysicsConstants.PhysTouch) {
					coll.IsContact = true;
					coll.HitNormal.Set(_normal);
					coll.HitOrgNormalVelocity = bnv; // remember original normal velocity
					coll.HitDistance = bnd;
					return 0.0f; // hit time is ignored for contacts
				}
				return -1.0f; // large distance, small velocity -> no hit
			}

			var hitTime = bnd / (-bnv);
			if (hitTime < 0) {
				hitTime = 0.0f; // already penetrating? then collide immediately
			}


			if (float.IsNaN(hitTime) || float.IsInfinity(hitTime) || hitTime < 0 || hitTime > dTime) {
				// time is outside this frame ... no collision
				return -1.0f;
			}

			coll.HitNormal.Set(_normal);
			coll.HitDistance = bnd; // actual contact distance

			return hitTime;
		}

		public override void Collide(CollisionEvent coll, PlayerPhysics physics)
		{
			coll.Ball.Hit.Collide3DWall(coll.HitNormal, Elasticity, ElasticityFalloff, Friction,
				Scatter);

			// distance from plane to ball surface
			var bnd = _normal.Dot(coll.Ball.State.Pos) - coll.Ball.Data.Radius - _d;
			if (bnd < 0)
			{
				// if ball has penetrated, push it out of the plane
				var v = _normal.Clone().MultiplyScalar(bnd);
				coll.Ball.State.Pos.Add(v);
			}
		}
	}
}
