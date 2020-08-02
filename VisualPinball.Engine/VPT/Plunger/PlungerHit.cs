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
		public readonly float Position;
		public readonly float FrameTop;
		public readonly float FrameBottom;
		public readonly float FrameLen;
		public readonly float RestPos;

		private readonly PlungerData _data;
		private readonly float _zHeight;
		private readonly float _x;
		private readonly float _y;
		private readonly float _x2;

		public PlungerHit(PlungerData data, float zHeight) : base(ItemType.Plunger)
		{
			_data = data;
			_zHeight = zHeight;
			_x = _data.Center.X - _data.Width;
			_y = _data.Center.Y + _data.Height;
			_x2 = _data.Center.X + _data.Width;

			FrameTop = data.Center.Y - data.Stroke;
			FrameBottom = data.Center.Y;
			FrameLen = FrameBottom - FrameTop;
			RestPos = data.ParkPosition;
			Position = FrameTop + RestPos * FrameLen;

			// static
			LineSegBase = new LineSeg(new Vertex2D(_x, _y), new Vertex2D(_x2, _y), zHeight, zHeight + Plunger.PlungerHeight, ItemType.Plunger);
			JointBase[0] = new HitLineZ(new Vertex2D(_x, _y), zHeight, zHeight + Plunger.PlungerHeight, ItemType.Plunger);
			JointBase[1] = new HitLineZ(new Vertex2D(_x2, _y), zHeight, zHeight + Plunger.PlungerHeight, ItemType.Plunger);

			// dynamic
			LineSegSide[0] = new LineSeg(new Vertex2D(_x + 0.0001f, Position), new Vertex2D(_x, _y), zHeight, zHeight + Plunger.PlungerHeight, ItemType.Plunger);
			LineSegSide[1] = new LineSeg(new Vertex2D(_x2, _y), new Vertex2D(_x2 + 0.0001f, Position), zHeight, zHeight + Plunger.PlungerHeight, ItemType.Plunger);
			LineSegEnd = new LineSeg(new Vertex2D(_x2, Position), new Vertex2D(_x, Position), zHeight, zHeight + Plunger.PlungerHeight, ItemType.Plunger);
			JointEnd[0] = new HitLineZ(new Vertex2D(_x, Position), zHeight, zHeight + Plunger.PlungerHeight, ItemType.Plunger);
			JointEnd[1] = new HitLineZ(new Vertex2D(_x2, Position), zHeight, zHeight + Plunger.PlungerHeight, ItemType.Plunger);
		}

		public override void SetIndex(int index, int version)
		{
			base.SetIndex(index, version);
			LineSegBase.SetIndex(index, version);
			LineSegEnd.SetIndex(index, version);
			LineSegSide[0].SetIndex(index, version);
			LineSegSide[1].SetIndex(index, version);
			JointEnd[0].SetIndex(index, version);
			JointEnd[1].SetIndex(index, version);
			JointBase[0].SetIndex(index, version);
			JointBase[1].SetIndex(index, version);
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
