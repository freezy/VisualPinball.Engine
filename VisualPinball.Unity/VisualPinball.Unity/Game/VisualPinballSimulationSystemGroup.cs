using System.Collections.Generic;
using NLog;
using Unity.Entities;
using Unity.Transforms;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Physics;

namespace VisualPinball.Unity.Game
{
	/// <summary>
	/// Main physics simulation system, executed once per frame.
	/// </summary>
	[UpdateBefore(typeof(TransformSystemGroup))]
	public class VisualPinballSimulationSystemGroup : ComponentSystemGroup
	{
		public double PhysicsDiffTime;

		public override IEnumerable<ComponentSystemBase> Systems => _systemsToUpdate;

		private long _nextPhysicsFrameTime;
		private long _currentPhysicsTime;

		private long _lastUpdatePhysicsUsec;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly List<ComponentSystemBase> _systemsToUpdate = new List<ComponentSystemBase>();
		private VisualPinballUpdateVelocitiesSystemGroup _velocitiesSystemGroup;
		private VisualPinballSimulatePhysicsCycleSystemGroup _cycleSystemGroup;
		private VisualPinballTransformSystemGroup _transformSystemGroup;

		protected override void OnCreate()
		{
			_velocitiesSystemGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballUpdateVelocitiesSystemGroup>();
			_cycleSystemGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballSimulatePhysicsCycleSystemGroup>();
			_transformSystemGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballTransformSystemGroup>();

			_systemsToUpdate.Add(_velocitiesSystemGroup);
			_systemsToUpdate.Add(_cycleSystemGroup);
			_systemsToUpdate.Add(_transformSystemGroup);
		}

		protected override void OnUpdate()
		{
			const int startTimeUsec = 0;
			var initialTimeUsec = (long)(Time.ElapsedTime * 1000000);
			var curPhysicsFrameTime = _currentPhysicsTime == 0
				? (long) (initialTimeUsec - Time.DeltaTime * 1000000)
				: _currentPhysicsTime;

			var tt = initialTimeUsec - startTimeUsec;
			//Logger.Info("[{0}] (+{1}) Player::UpdatePhysics()\n", tt, (double)(initialTimeUsec - _lastUpdatePhysicsUsec) / 1000);
			_lastUpdatePhysicsUsec = initialTimeUsec;

			while (curPhysicsFrameTime < initialTimeUsec) {

				var timeMsec = (int)((curPhysicsFrameTime - startTimeUsec) / 1000);

				PhysicsDiffTime = (_nextPhysicsFrameTime - curPhysicsFrameTime) * (1.0 / PhysicsConstants.DefaultStepTime);

				//Logger.Info($"   [{timeMsec}] ({PhysicsDiffTime}) loop");

				// update velocities
				_velocitiesSystemGroup.Update();

				// simulate cycle
				_cycleSystemGroup.Update();

				// new cycle, on physics frame boundary
				curPhysicsFrameTime = _nextPhysicsFrameTime;

				// advance physics position
				_nextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;
			}
			_currentPhysicsTime = curPhysicsFrameTime;

			_transformSystemGroup.Update();
		}
	}
}
