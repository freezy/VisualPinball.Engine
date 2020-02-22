using System;
using VisualPinball.Engine.VPT.Ball;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Game
{
	public class Player
	{
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
			return _physics.CreateBall(ballCreator, this, radius, mass);
		}

		private void SetupTableElements()
		{
			foreach (var playable in _table.Playables) {
				playable.SetupPlayer(this, _table);
			}
		}
	}
}
