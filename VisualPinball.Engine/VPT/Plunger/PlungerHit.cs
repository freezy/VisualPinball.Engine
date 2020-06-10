using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Plunger
{
	public class PlungerHit : HitObject
	{
		public readonly LineSeg LineSegBase;
		public readonly LineSeg LineSegEnd;
		public readonly LineSeg[] LineSegSide = new LineSeg[2];
		public readonly HitLineZ[] JointBase = new HitLineZ[2];
		public readonly HitLineZ[] JointEnd  = new HitLineZ[2];

		private readonly PlungerData _data;
		private readonly float _zHeight;
		private readonly float _x;
		private readonly float _y;
		private readonly float _x2;

		public PlungerHit(PlungerData data, float zHeight)
		{
			_data = data;
			_zHeight = zHeight;
			_x = _data.Center.X - _data.Width;
			_y = _data.Center.Y + _data.Height;
			_x2 = _data.Center.X + _data.Width;

			LineSegBase = new LineSeg(new Vertex2D(_x, _y), new Vertex2D(_x2, _y), zHeight, zHeight + Plunger.PlungerHeight);
			JointBase[0] = new HitLineZ(new Vertex2D(_x, _y), zHeight, zHeight + Plunger.PlungerHeight);
			JointBase[1] = new HitLineZ(new Vertex2D(_x2, _y), zHeight, zHeight + Plunger.PlungerHeight);
		}

		public override void CalcHitBBox()
		{

			var frameEnd = _data.Center.Y - _data.Stroke;
			HitBBox = new Rect3D(
				_x - 0.1f,
				_x2 + 0.1f,
				frameEnd - 0.1f,
				_y + 0.1f,
				_zHeight,
				_zHeight + Plunger.PlungerHeight
			);
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
