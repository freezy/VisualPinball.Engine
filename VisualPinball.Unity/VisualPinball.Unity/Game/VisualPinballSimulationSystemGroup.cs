using System;
using System.Collections.Generic;
using System.Diagnostics;
using NLog;
using Unity.Entities;
using Unity.Transforms;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Main physics simulation system, executed once per frame.
	/// </summary>
	[UpdateBefore(typeof(TransformSystemGroup))]
	public class VisualPinballSimulationSystemGroup : ComponentSystemGroup
	{
		public double PhysicsDiffTime;
		public double CurrentPhysicsTime => _currentPhysicsTime * (1.0 / PhysicsConstants.DefaultStepTime);

		public uint TimeMsec;

		public override IEnumerable<ComponentSystemBase> Systems => _systemsToUpdate;

		private readonly Stopwatch _time = new Stopwatch();
		private ulong _currentPhysicsTime;
		private ulong _currentPhysicsFrameTime;
		private ulong _nextPhysicsFrameTime;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly List<ComponentSystemBase> _systemsToUpdate = new List<ComponentSystemBase>();
		private CreateBallEntityCommandBufferSystem _createBallEntityCommandBufferSystem;
		private UpdateVelocitiesSystemGroup _velocitiesSystemGroup;
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private BallRingCounterSystem _ballRingCounterSystem;
		private UpdateAnimationsSystemGroup _updateAnimationsSystemGroup;
		private TransformMeshesSystemGroup _transformMeshesSystemGroup;

		private const TimingMode Timing = TimingMode.UnityTime;

		protected override void OnCreate()
		{
			// let IPhysicsEngine enable it
			Enabled = false;

			_time.Start();

			_createBallEntityCommandBufferSystem = World.GetOrCreateSystem<CreateBallEntityCommandBufferSystem>();
			_velocitiesSystemGroup = World.GetOrCreateSystem<UpdateVelocitiesSystemGroup>();
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
			_ballRingCounterSystem = World.GetOrCreateSystem<BallRingCounterSystem>();
			_updateAnimationsSystemGroup = World.GetOrCreateSystem<UpdateAnimationsSystemGroup>();
			_transformMeshesSystemGroup = World.GetOrCreateSystem<TransformMeshesSystemGroup>();

			_systemsToUpdate.Add(_createBallEntityCommandBufferSystem);
			_systemsToUpdate.Add(_velocitiesSystemGroup);
			_systemsToUpdate.Add(_simulateCycleSystemGroup);
			_systemsToUpdate.Add(_ballRingCounterSystem);
			_systemsToUpdate.Add(_updateAnimationsSystemGroup);
			_systemsToUpdate.Add(_transformMeshesSystemGroup);
			base.OnCreate();
		}

		protected override void OnStartRunning()
		{
			_currentPhysicsTime = GetTargetTime();
			_nextPhysicsFrameTime = _currentPhysicsTime + PhysicsConstants.PhysicsStepTime;
		}

		protected override void OnUpdate()
		{
			_createBallEntityCommandBufferSystem.Update();

			//const int startTimeUsec = 0;
			var initialTimeUsec = GetTargetTime();

			while (_currentPhysicsFrameTime < initialTimeUsec) {

				TimeMsec = (uint) (Time.ElapsedTime * 1000);
				PhysicsDiffTime = (_nextPhysicsFrameTime - _currentPhysicsFrameTime) * (1.0 / PhysicsConstants.DefaultStepTime);

				// update velocities
				_velocitiesSystemGroup.Update();

				// simulate cycle
				_simulateCycleSystemGroup.Update();

				// new cycle, on physics frame boundary
				_currentPhysicsFrameTime = _nextPhysicsFrameTime;

				// advance physics position
				_nextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;
			}

			_ballRingCounterSystem.Update();

			_currentPhysicsTime = _currentPhysicsFrameTime;

			// update animations
			_updateAnimationsSystemGroup.Update();

			// transform all meshes
			_transformMeshesSystemGroup.Update();
		}

		private ulong GetTargetTime()
		{
			const long dt60fps = 1000000 / 60;

			switch (Timing) {
				case TimingMode.Atleast60:
					var dt = (ulong)(Time.DeltaTime * 1000000);
					if (_currentPhysicsTime > 0 && dt > dt60fps) {
						dt = dt60fps;
					}
					return _currentPhysicsTime + dt;

				case TimingMode.Locked60:
					return _currentPhysicsTime + dt60fps;

				case TimingMode.UnityTime:
					return (ulong)(Time.ElapsedTime * 1000000);

				case TimingMode.SystemTime:
					return (ulong) (_time.Elapsed.TotalMilliseconds * 1000);

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private enum TimingMode
		{
			UnityTime,
			SystemTime,
			Atleast60,
			Locked60
		};
	}

}
