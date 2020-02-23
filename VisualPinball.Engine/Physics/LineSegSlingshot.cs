using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Engine.Physics
{
	public class LineSegSlingshot : LineSeg
	{
		public float Force = 0;
		public bool DoHitEvent = false;

		private readonly SurfaceData _surfaceData;
		private readonly SlingshotAnimObject _slingshotAnim = new SlingshotAnimObject();
		private float _eventTimeReset = 0;

		public LineSegSlingshot(SurfaceData surfaceData, Vertex2D p1, Vertex2D p2, float zLow, float zHigh)
			: base(p1, p2, zLow, zHigh)
		{
			_surfaceData = surfaceData;
		}

		public override void Collide(CollisionEvent coll, PlayerPhysics physics)
		{
			var ball = coll.Ball;
			var hitNormal = coll.HitNormal;

			var dot = coll.HitNormal.Dot(coll.Ball.Hit.Vel); // normal velocity to slingshot
			var threshold = dot <= -_surfaceData.SlingshotThreshold; // normal greater than threshold?

			if (!_surfaceData.IsDisabled && threshold) {
				// enabled and if velocity greater than threshold level
				var len = (V2.X - V1.X) * hitNormal.Y - (V2.Y - V1.Y) * hitNormal.X; // length of segment, Unit TAN points from V1 to V2

				var vHitPoint = new Vertex2D(
					ball.State.Pos.X - hitNormal.X * ball.Data.Radius, // project ball radius along norm
					ball.State.Pos.Y - hitNormal.Y * ball.Data.Radius
				);

				// vHitPoint will now be the point where the ball hits the line
				// Calculate this distance from the center of the slingshot to get force
				var btd = (vHitPoint.X - V1.X) * hitNormal.Y - (vHitPoint.Y - V1.Y) * hitNormal.X; // distance to vhit from V1
				var force = MathF.Abs(len) > 1.0e-6 ? (btd + btd) / len - 1.0f : -1.0f; // -1..+1
				force = 0.5f * (1.0f - force * force); // !! maximum value 0.5 ...I think this should have been 1.0...Oh well
				// will match the previous physics
				force *= Force; //-80;

				// boost velocity, drive into slingshot (counter normal), allow CollideWall to handle the remainder
				var normForce = hitNormal.Clone().MultiplyScalar(force);
				ball.Hit.Vel.Sub(normForce);
			}

			ball.Hit.Collide3DWall(hitNormal, Elasticity, ElasticityFalloff, Friction, Scatter);

			if (Obj != null && FireEvents && !_surfaceData.IsDisabled && Threshold != 0) {
				// is this the same place as last event? if same then ignore it
				var eventPos = ball.Hit.EventPos.Clone();
				var distLs = eventPos.Sub(ball.State.Pos).LengthSq();
				ball.Hit.EventPos.Set(ball.State.Pos); //remember last collide position

				if (distLs > 0.25) {
					// must be a new place if only by a little
					Obj.FireGroupEvent(Event.SurfaceEventsSlingshot);
					_slingshotAnim.TimeReset = physics.TimeMsec + 100;
				}
			}
		}
	}
}
