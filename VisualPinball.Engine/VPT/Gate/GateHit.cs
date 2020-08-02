using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Gate
{
	public class GateHit : HitObject
	{
		public LineSeg LineSeg0;
		public LineSeg LineSeg1;
		public bool TwoWay;

		public GateHit(GateData data, float height) : base(ItemType.Gate)
		{
			var data1 = data;
			var height1 = height;

			var halfLength = data1.Length * 0.5f;
			var radAngle = MathF.DegToRad(data1.Rotation);
			var sn = MathF.Sin(radAngle);
			var cs = MathF.Cos(radAngle);

			LineSeg0 = new LineSeg(
				new Vertex2D(
					data1.Center.X - cs * (halfLength + PhysicsConstants.PhysSkin),
					data1.Center.Y - sn * (halfLength + PhysicsConstants.PhysSkin)
				),
				new Vertex2D(
					data1.Center.X + cs * (halfLength + PhysicsConstants.PhysSkin),
					data1.Center.Y + sn * (halfLength + PhysicsConstants.PhysSkin)
				),
				height1,
				height1 + 2.0f * PhysicsConstants.PhysSkin,
				ItemType.Gate
			);

			LineSeg1 = new LineSeg(
				new Vertex2D(LineSeg0.V2.X, LineSeg0.V2.Y),
				new Vertex2D(LineSeg0.V1.X, LineSeg0.V1.Y),
				height,
				height + 2.0f * PhysicsConstants.PhysSkin,
				ItemType.Gate
			);

			TwoWay = false;
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
