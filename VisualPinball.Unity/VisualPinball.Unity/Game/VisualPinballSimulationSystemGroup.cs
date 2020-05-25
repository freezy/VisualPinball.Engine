using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NLog;
using Unity.Entities;
using Unity.Transforms;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Physics;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Game
{
	/// <summary>
	/// Main physics simulation system, executed once per frame.
	/// </summary>
	[UpdateBefore(typeof(TransformSystemGroup))]
	public class VisualPinballSimulationSystemGroup : ComponentSystemGroup
	{
		public double PhysicsDiffTime;
		public int NumBalls;

		public override IEnumerable<ComponentSystemBase> Systems => _systemsToUpdate;

		private readonly Stopwatch _time = new Stopwatch();
		private readonly Stopwatch _frameTime = new Stopwatch();
		private ulong _currentPhysicsTime;
		private ulong _nextPhysicsFrameTime;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly List<ComponentSystemBase> _systemsToUpdate = new List<ComponentSystemBase>();
		private CreateBallEntityCommandBufferSystem _createBallEntityCommandBufferSystem;
		private UpdateVelocitiesSystemGroup _velocitiesSystemGroup;
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private TransformMeshesSystemGroup _transformMeshesSystemGroup;
		public ulong CurPhysicsFrameTime;
		private ulong _lastUpdatePhysicsUsec;

		#if TIME_LOG
		private StringBuilder _log;
		public ulong StartLogTimeUsec;
		#endif

		protected override void OnCreate()
		{
			_time.Start();

			_createBallEntityCommandBufferSystem = World.GetOrCreateSystem<CreateBallEntityCommandBufferSystem>();
			_velocitiesSystemGroup = World.GetOrCreateSystem<UpdateVelocitiesSystemGroup>();
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
			_transformMeshesSystemGroup = World.GetOrCreateSystem<TransformMeshesSystemGroup>();

			_systemsToUpdate.Add(_createBallEntityCommandBufferSystem);
			_systemsToUpdate.Add(_velocitiesSystemGroup);
			_systemsToUpdate.Add(_simulateCycleSystemGroup);
			_systemsToUpdate.Add(_transformMeshesSystemGroup);

			#if TIME_LOG
			_log = new StringBuilder();
			#endif
		}

		protected override void OnStartRunning()
		{
			_currentPhysicsTime = GetTargetTime();
			_nextPhysicsFrameTime = _currentPhysicsTime + PhysicsConstants.PhysicsStepTime;
		}

		#if TIME_LOG
		protected override void OnDestroy()
		{
			var f = new StreamWriter(@"C:\Development\vpvr\m_flog-unity.txt");
			f.Write(_log.ToString());
			f.Dispose();
		}

		public void Log(string line)
		{
			_log.AppendLine(line);
		}
		#endif

		private enum TimingMode { UnityTime, SystemTime, Atleast60, Locked60 };

		private const TimingMode timingMode = TimingMode.UnityTime;

		private ulong GetTargetTime()
		{
			const long dt60fps = 1000000 / 60;

			switch (timingMode) {
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

		protected override void OnUpdate()
		{
			#if TIME_LOG
			_frameTime.Reset();
			_frameTime.Start();
			#endif

			_createBallEntityCommandBufferSystem.Update();

			//const int startTimeUsec = 0;
			var initialTimeUsec = GetTargetTime();

			#if TIME_LOG
			if (StartLogTimeUsec == 0 && NumBalls > 0) {
				StartLogTimeUsec = initialTimeUsec;
				Log($"Logging physics time, because we now have {NumBalls} ball(s).");
			}

			if (StartLogTimeUsec > 0) {
				var tt = initialTimeUsec - StartLogTimeUsec;
				Log($"[{(double) tt / 1000}] (+{(double) (initialTimeUsec - _lastUpdatePhysicsUsec) / 1000}) Player::UpdatePhysics()");
			}
			#endif

			_lastUpdatePhysicsUsec = initialTimeUsec;

			while (CurPhysicsFrameTime < initialTimeUsec) {

				PhysicsDiffTime = (_nextPhysicsFrameTime - CurPhysicsFrameTime) * (1.0 / PhysicsConstants.DefaultStepTime);

				#if TIME_LOG
				if (StartLogTimeUsec > 0) {
					var timeMsecLog = (int) ((CurPhysicsFrameTime - StartLogTimeUsec) / 1000);
					Log($"   [{timeMsecLog}] ({PhysicsDiffTime}) outer loop");
				}
				#endif

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

			#if TIME_LOG
			_frameTime.Stop();
			var ms = Math.Round(_frameTime.Elapsed.TotalMilliseconds, 3);
			Log($"Frame: {ms}ms");
			#endif
		}
	}
}
