// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Entities;
using Unity.Transforms;
using VisualPinball.Engine.Common;

namespace VisualPinballUnity
{
	/// <summary>
	/// Main physics simulation system, executed once per frame.
	/// </summary>
	[UpdateBefore(typeof(TransformSystemGroup))]
	internal partial class VisualPinballSimulationSystemGroup : ComponentSystemGroup
	{
		public double PhysicsDiffTime;
		public double CurrentPhysicsTime => _currentPhysicsTime * (1.0 / PhysicsConstants.DefaultStepTime);
		public uint TimeMsec;

		public override IReadOnlyList<ComponentSystemBase> Systems => _systemsToUpdate;

		private readonly Stopwatch _time = new Stopwatch();
		private ulong _currentPhysicsTime;
		private ulong _currentPhysicsFrameTime;
		private ulong _nextPhysicsFrameTime;

		private readonly List<ComponentSystemBase> _systemsToUpdate = new List<ComponentSystemBase>();
		private CreateBallEntityCommandBufferSystem _createBallEntityCommandBufferSystem;
		private UpdateVelocitiesSystemGroup _velocitiesSystemGroup;
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private BallRingCounterSystem _ballRingCounterSystem;
		private UpdateAnimationsSystemGroup _updateAnimationsSystemGroup;
		private TransformMeshesSystemGroup _transformMeshesSystemGroup;

		private readonly Queue<Action> _afterBallCreationQueue = new Queue<Action>();
		private readonly Queue<Action> _beforeBallCreationQueue = new Queue<Action>();
		private readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();

		private const TimingMode Timing = TimingMode.UnityTime;

		protected override void OnCreate()
		{
			// let IPhysicsEngine enable it
			Enabled = false;

			_time.Start();

			_createBallEntityCommandBufferSystem = World.GetOrCreateSystemManaged<CreateBallEntityCommandBufferSystem>();
			_velocitiesSystemGroup = World.GetOrCreateSystemManaged<UpdateVelocitiesSystemGroup>();
			_simulateCycleSystemGroup = World.GetOrCreateSystemManaged<SimulateCycleSystemGroup>();
			// todo re-enable system
			// _ballRingCounterSystem = World.GetOrCreateSystemManaged<BallRingCounterSystem>();
			_updateAnimationsSystemGroup = World.GetOrCreateSystemManaged<UpdateAnimationsSystemGroup>();
			_transformMeshesSystemGroup = World.GetOrCreateSystemManaged<TransformMeshesSystemGroup>();

			_systemsToUpdate.Add(_createBallEntityCommandBufferSystem);
			_systemsToUpdate.Add(_velocitiesSystemGroup);
			_systemsToUpdate.Add(_simulateCycleSystemGroup);
			// todo re-enable system
			// _systemsToUpdate.Add(_ballRingCounterSystem);
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
			lock (_beforeBallCreationQueue) {
				while (_beforeBallCreationQueue.Count > 0) {
					_beforeBallCreationQueue.Dequeue().Invoke();
				}
			}
			_createBallEntityCommandBufferSystem.Update();
			lock (_afterBallCreationQueue) {
				while (_afterBallCreationQueue.Count > 0) {
					_afterBallCreationQueue.Dequeue().Invoke();
				}
			}

			//const int startTimeUsec = 0;
			var initialTimeUsec = GetTargetTime();

			while (_currentPhysicsFrameTime < initialTimeUsec) {

				TimeMsec = (uint) (SystemAPI.Time.ElapsedTime * 1000);
				PhysicsDiffTime = (_nextPhysicsFrameTime - _currentPhysicsFrameTime) * (1.0 / PhysicsConstants.DefaultStepTime);

				// update velocities
				_velocitiesSystemGroup.Update();

				// simulate cycle
				_simulateCycleSystemGroup.Update();

				// new cycle, on physics frame boundary
				_currentPhysicsFrameTime = _nextPhysicsFrameTime;

				// advance physics position
				_nextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;

				// run scheduled actions
				lock (_scheduledActions) {
					for (var i = _scheduledActions.Count - 1; i >= 0; i--) {
						if (_currentPhysicsFrameTime > _scheduledActions[i].ScheduleAt) {
							_scheduledActions[i].Action();
							_scheduledActions.RemoveAt(i);
						}
					}
				}
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
					var dt = (ulong)(SystemAPI.Time.DeltaTime * 1000000);
					if (_currentPhysicsTime > 0 && dt > dt60fps) {
						dt = dt60fps;
					}
					return _currentPhysicsTime + dt;

				case TimingMode.Locked60:
					return _currentPhysicsTime + dt60fps;

				case TimingMode.UnityTime:
					return (ulong)(SystemAPI.Time.ElapsedTime * 1000000);

				case TimingMode.SystemTime:
					return (ulong) (_time.Elapsed.TotalMilliseconds * 1000);

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public void QueueBeforeBallCreation(Action action)
		{
			lock (_beforeBallCreationQueue) {
				_beforeBallCreationQueue.Enqueue(action);
			}
		}

		public void QueueAfterBallCreation(Action action)
		{
			lock (_afterBallCreationQueue) {
				_afterBallCreationQueue.Enqueue(action);
			}
		}

		public void ScheduleAction(int timeoutMs, Action action) => ScheduleAction((uint)timeoutMs, action);
		public void ScheduleAction(uint timeoutMs, Action action)
		{
			lock (_scheduledActions) {
				_scheduledActions.Add(new ScheduledAction(_currentPhysicsFrameTime + (ulong)timeoutMs * 1000, action));
			}
		}

		private enum TimingMode
		{
			UnityTime,
			SystemTime,
			Atleast60,
			Locked60
		}

		private class ScheduledAction
		{
			public readonly ulong ScheduleAt;
			public readonly Action Action;

			public ScheduledAction(ulong scheduleAt, Action action)
			{
				ScheduleAt = scheduleAt;
				Action = action;
			}
		}
	}

}
