using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Plunger
{
	public class PlungerHit : HitObject
	{
		private readonly PlungerData _data;
		private readonly float _zHeight;

		public PlungerHit(PlungerData data, float zHeight)
		{
			_data = data;
			_zHeight = zHeight;
		}

		public override void CalcHitBBox()
		{
			var x = _data.Center.X - _data.Width;
			var y = _data.Center.Y + _data.Height;
			var x2 = _data.Center.X + _data.Width;
			var frameEnd = _data.Center.Y - _data.Stroke;
			HitBBox = new Rect3D(
				x - 0.1f,
				x2 + 0.1f,
				frameEnd - 0.1f,
				y + 0.1f,
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
