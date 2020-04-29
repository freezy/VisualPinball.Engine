using System.Collections.Generic;
using NLog;
using Unity.Entities;
using Unity.Transforms;
using VisualPinball.Engine.Common;
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
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private TransformMeshesSystemGroup _transformMeshesSystemGroup;
		public long CurPhysicsFrameTime;

		protected override void OnCreate()
		{
			_velocitiesSystemGroup = World.GetOrCreateSystem<UpdateVelocitiesSystemGroup>();
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
			_transformMeshesSystemGroup = World.GetOrCreateSystem<TransformMeshesSystemGroup>();

			_systemsToUpdate.Add(_velocitiesSystemGroup);
			_systemsToUpdate.Add(_simulateCycleSystemGroup);
			_systemsToUpdate.Add(_transformMeshesSystemGroup);
		}

		enum TimingMode { RealTime, Atleast60, Locked60 };
		TimingMode timingMode = TimingMode.Locked60;

		long GetTargetTime()
		{
			const long dt60fps = 1000000 / 60;
			long t = (long)(Time.ElapsedTime * 1000000); // default: TimingMode.RealTime:

			switch (timingMode)
			{
				case TimingMode.Atleast60:
					long dt = (long)(Time.DeltaTime * 1000000);
					if (_currentPhysicsTime > 0 && dt > dt60fps)
					{
						dt = dt60fps;
					}
					t = _currentPhysicsTime + dt;					
					break;

				case TimingMode.Locked60:
					t = _currentPhysicsTime + dt60fps;
					break;
			}
			return t;
		}

		protected override void OnUpdate()
		{
			const int startTimeUsec = 0;
			var initialTimeUsec = GetTargetTime();
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
				_simulateCycleSystemGroup.Update();

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
