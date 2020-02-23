using System;
using System.Collections.Generic;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Ball;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Game
{
	public class Player
	{
		public IEnumerable<Ball> Balls => _physics.Balls.ToArray();
		public event EventHandler<BallCreationArgs> BallCreated;
		public event EventHandler<BallDestructionArgs> BallDestroyed;

		private readonly Table _table;
		private readonly PlayerPhysics _physics;
		private bool _isInitialized;
		private double _simulatedTimeMs;

		public Player(Table table)
		{
			_table = table;
			_physics = new PlayerPhysics(table);
			SetupTableElements();
		}

		public Player Init()
		{
			_physics.Init();
			_isInitialized = true;
			return this;
		}

		public void SimulateTime(long dTimeMs, int fps = 60)
		{
			if (!_isInitialized) {
				throw new InvalidOperationException("Player must be initialized before simulating time.");
			}
			var timePerFrameMs = (double)1000 / fps;
			while (_simulatedTimeMs <= dTimeMs) {
				_physics.UpdatePhysics((long)(_simulatedTimeMs * 1000));
				// todo this.UpdateAnimations(SimulatedTimeMs);
				_simulatedTimeMs += timePerFrameMs;
			}
		}

		public void UpdatePhysics() => _physics.UpdatePhysics();
		public void UpdatePhysics(float dTime) => _physics.UpdatePhysics((long)(dTime * 1000));

		public Ball CreateBall(IBallCreationPosition ballCreator, float radius = 25f, float mass = 1)
		{
			var ball = _physics.CreateBall(ballCreator, this, radius, mass);
			BallCreated?.Invoke(this, new BallCreationArgs(ball.Name, ball.State.Pos, radius));
			return ball;
		}

		private void SetupTableElements()
		{
			foreach (var playable in _table.Playables) {
				playable.SetupPlayer(this, _table);
			}
		}
	}

	public class BallCreationArgs : EventArgs
	{
		public string Name { get; }
		public Vertex3D Position { get; }
		public float Radius { get; }

		public BallCreationArgs(string name, Vertex3D position, float radius)
		{
			Name = name;
			Position = position;
			Radius = radius;
		}
	}

	public class BallDestructionArgs : EventArgs
	{

	}
}
