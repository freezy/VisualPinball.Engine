using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public class DebugBallCreator : IBallCreationPosition
	{
		private float _x;
		private float _y;

		private readonly float _kickAngle;
		private readonly float _kickForce;

		public DebugBallCreator()
		{
			_x = -1;
			_y = -1;
		}

		public DebugBallCreator(float x, float y)
		{
			_x = x;
			_y = y;
		}

		public DebugBallCreator(float x, float y, float kickAngle, float kickForce)
		{
			_x = x;
			_y = y;
			_kickAngle = kickAngle;
			_kickForce = kickForce;
		}

		public Vertex3D GetBallCreationPosition(Table table)
		{
			if (_x < 0 || _y < 0) {
				_x = Random.Range(table.Width / 6f, table.Width / 6f * 5f);
				_y = Random.Range(table.Height / 8f, table.Height / 2f);
			}
			return new Vertex3D(_x, _y, 0);
		}

		public Vertex3D GetBallCreationVelocity(Table table)
		{
			return new Vertex3D(
				MathF.Sin(_kickAngle) * _kickForce,
				-MathF.Cos(_kickAngle) * _kickForce,
				0
			);
		}

		public void OnBallCreated(PlayerPhysics physics, Ball ball)
		{
			// nothing to do
		}
	}
}
