using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Spinner
{
	public class SpinnerHit : HitObject
	{
		public LineSeg LineSeg0;
		public LineSeg LineSeg1;

		public SpinnerHit(SpinnerData data, float height) : base(ItemType.Spinner)
		{
			var halfLength = data.Length * 0.5f;

			var radAngle = MathF.DegToRad(data.Rotation);
			var sn = MathF.Sin(radAngle);
			var cs = MathF.Cos(radAngle);

			var v1 = new Vertex2D(
				data.Center.X - cs * (halfLength + PhysicsConstants.PhysSkin), // through the edge of the
				data.Center.Y - sn * (halfLength + PhysicsConstants.PhysSkin)  // spinner
			);
			var v2 = new Vertex2D(
				data.Center.X + cs * (halfLength + PhysicsConstants.PhysSkin), // oversize by the ball radius
				data.Center.Y + sn * (halfLength + PhysicsConstants.PhysSkin)  // this will prevent clipping
			);

			LineSeg0 = new LineSeg(v1, v2, height, height + 2.0f * PhysicsConstants.PhysSkin, ItemType.Spinner);
			LineSeg1 = new LineSeg(v2.Clone(), v1.Clone(), height, height + 2.0f * PhysicsConstants.PhysSkin, ItemType.Spinner);
		}

		public override void SetIndex(int index, int version)
		{
			base.SetIndex(index, version);
			LineSeg0.SetIndex(index, version);
			LineSeg1.SetIndex(index, version);
		}

		public override void CalcHitBBox()
		{
			LineSeg0.CalcHitBBox();
			HitBBox = LineSeg0.HitBBox;
		}

		public override float HitTest(Ball.Ball ball, float dTime, CollisionEvent coll, PlayerPhysics physics)
		{
			// not needed in unity ECS
			throw new System.NotImplementedException();
		}

		public override void Collide(CollisionEvent coll, PlayerPhysics physics)
		{
			// not needed in unity ECS
			throw new System.NotImplementedException();
		}
	}
}
