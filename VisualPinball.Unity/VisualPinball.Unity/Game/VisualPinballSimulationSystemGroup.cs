using System.Collections.Generic;
using NLog;
using Unity.Entities;
using Unity.Transforms;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Physics;
using VisualPinball.Unity.Physics.SystemGroup;

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
		private UpdateVelocitiesSystemGroup _velocitiesSystemGroup;
		private SimulateCycleSystemGroup _cycleSystemGroup;
		private TransformMeshesSystemGroup _transformMeshesSystemGroup;
		public long CurPhysicsFrameTime;

		protected override void OnCreate()
		{
			_velocitiesSystemGroup = World.GetOrCreateSystem<UpdateVelocitiesSystemGroup>();
			_cycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
			_transformMeshesSystemGroup = World.GetOrCreateSystem<TransformMeshesSystemGroup>();

			_systemsToUpdate.Add(_velocitiesSystemGroup);
			_systemsToUpdate.Add(_cycleSystemGroup);
			_systemsToUpdate.Add(_transformMeshesSystemGroup);
		}

		protected override void OnUpdate()
		{
			const int startTimeUsec = 0;
			var initialTimeUsec = (long)(Time.ElapsedTime * 1000000);
			CurPhysicsFrameTime = _currentPhysicsTime == 0
				? (long) (initialTimeUsec - Time.DeltaTime * 1000000)
				: _currentPhysicsTime;

			var tt = initialTimeUsec - startTimeUsec;
			//Logger.Info("[{0}] (+{1}) Player::UpdatePhysics()\n", tt, (double)(initialTimeUsec - _lastUpdatePhysicsUsec) / 1000);
			_lastUpdatePhysicsUsec = initialTimeUsec;

			while (CurPhysicsFrameTime < initialTimeUsec) {

				var timeMsec = (int)((CurPhysicsFrameTime - startTimeUsec) / 1000);

				PhysicsDiffTime = (_nextPhysicsFrameTime - CurPhysicsFrameTime) * (1.0 / PhysicsConstants.DefaultStepTime);

				//Logger.Info($"   [{timeMsec}] ({PhysicsDiffTime}) loop");

				// update velocities
				_velocitiesSystemGroup.Update();

				// simulate cycle
				_cycleSystemGroup.Update();

				// new cycle, on physics frame boundary
				CurPhysicsFrameTime = _nextPhysicsFrameTime;

				// advance physics position
				_nextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;
			}
			_currentPhysicsTime = CurPhysicsFrameTime;

			_transformMeshesSystemGroup.Update();
		}
	}
}
