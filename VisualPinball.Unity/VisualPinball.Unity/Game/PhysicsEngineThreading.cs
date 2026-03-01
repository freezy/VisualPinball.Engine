// Copyright (C) 2026 freezy and VPE Team
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

// ReSharper disable InconsistentNaming

using System;
using System.Diagnostics;
using System.Threading;
using Unity.Mathematics;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Threading-related methods for the physics engine, organized by thread
	/// affinity.
	///
	/// <para>This is a standalone class (not a partial of
	/// <see cref="PhysicsEngine"/>) that accesses all shared state through
	/// its <see cref="PhysicsEngineContext"/> reference. This makes the
	/// data dependencies explicit — every field access goes through
	/// <c>_ctx.FieldName</c>.</para>
	///
	/// <para><b>Ownership:</b> Created by <see cref="PhysicsEngine.Start"/>
	/// after the context is fully populated. Receives the context,
	/// player, kinematic collider components, and the world-to-playfield
	/// matrix as constructor arguments.</para>
	/// </summary>
	internal class PhysicsEngineThreading
	{
		private readonly PhysicsEngineContext _ctx;
		private readonly Player _player;
		private readonly ICollidableComponent[] _kinematicColliderComponents;
		private readonly float4x4 _worldToPlayfield;
		private readonly PhysicsMovements _physicsMovements = new();

		internal PhysicsEngineThreading(PhysicsEngineContext ctx, Player player,
			ICollidableComponent[] kinematicColliderComponents, float4x4 worldToPlayfield)
		{
			_ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
			_player = player;
			_kinematicColliderComponents = kinematicColliderComponents;
			_worldToPlayfield = worldToPlayfield;
		}

		#region Simulation Thread

		// ──────────────────────────────────────────────────────────────
		// Methods in this region execute on the SIMULATION THREAD
		// (or on the main thread in single-threaded mode).
		// They run inside PhysicsLock unless noted otherwise.
		// ──────────────────────────────────────────────────────────────

		/// <summary>
		/// Execute a single physics tick with external timing.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Simulation thread (called by
		/// <see cref="SimulationThread"/>).<br/>
		/// Acquires <c>PhysicsLock</c> for the duration of the tick.
		/// Does NOT apply movements to GameObjects — the main thread reads
		/// the triple-buffered <see cref="SimulationState"/> instead.
		/// </remarks>
		/// <param name="timeUsec">Current simulation time in microseconds</param>
		internal void ExecuteTick(ulong timeUsec)
		{
			if (!_ctx.IsInitialized) return;

			lock (_ctx.PhysicsLock) {
				ExecutePhysicsSimulation(timeUsec);
			}
		}

		/// <summary>
		/// Core physics simulation loop for the simulation thread.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Simulation thread (inside <c>PhysicsLock</c>).<br/>
		/// The single-threaded equivalent is
		/// <see cref="ExecutePhysicsUpdate"/>, which additionally drains
		/// events and applies movements.
		/// </remarks>
		private void ExecutePhysicsSimulation(ulong currentTimeUsec)
		{
			var sw = Stopwatch.StartNew();

			// Apply kinematic transform updates staged by main thread.
			ApplyPendingKinematicTransforms();

			var state = _ctx.CreateState();

			// Rebuild kinematic octree only when transforms have changed.
			if (_ctx.KinematicOctreeDirty) {
				PhysicsKinematics.RebuildOctree(ref _ctx.KinematicOctree, ref state);
				_ctx.KinematicOctreeDirty = false;
			}

			// process input
			ProcessInputActions(ref state);

			// run physics loop (Burst-compiled, thread-safe)
			PhysicsUpdate.Execute(
				ref state,
				ref _ctx.PhysicsEnv,
				ref _ctx.OverlappingColliders,
				ref _ctx.KinematicOctree,
				ref _ctx.BallOctree,
				ref _ctx.PhysicsCycle,
				currentTimeUsec
			);

			RecordPhysicsBusyTime(sw.ElapsedTicks);
		}

		/// <summary>
		/// Drain the input actions queue.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Simulation thread (inside <c>PhysicsLock</c>).
		/// Also called from main thread in single-threaded mode.
		/// </remarks>
		private void ProcessInputActions(ref PhysicsState state)
		{
			lock (_ctx.InputActionsLock) {
				while (_ctx.InputActions.Count > 0) {
					var action = _ctx.InputActions.Dequeue();
					action(ref state);
				}
			}
		}

		/// <summary>
		/// Apply kinematic transforms staged by the main thread into the
		/// physics state maps.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Simulation thread (inside <c>PhysicsLock</c>).<br/>
		/// Lock ordering: <c>PhysicsLock</c> (held) then
		/// <c>PendingKinematicLock</c> (inner).
		/// </remarks>
		private void ApplyPendingKinematicTransforms()
		{
			if (!_ctx.PendingKinematicTransforms.Ref.IsCreated) return;

			_ctx.UpdatedKinematicTransforms.Ref.Clear();

			lock (_ctx.PendingKinematicLock) {
				if (_ctx.PendingKinematicTransforms.Ref.Count() == 0) return;

				using var enumerator = _ctx.PendingKinematicTransforms.Ref.GetEnumerator();
				while (enumerator.MoveNext()) {
					var itemId = enumerator.Current.Key;
					var matrix = enumerator.Current.Value;
					_ctx.UpdatedKinematicTransforms.Ref[itemId] = matrix;
					_ctx.KinematicTransforms.Ref[itemId] = matrix;
				}
				_ctx.PendingKinematicTransforms.Ref.Clear();
				_ctx.KinematicOctreeDirty = true;
			}
		}

		/// <summary>
		/// Copy current animation values from physics state maps into the
		/// given snapshot buffer. Must be allocation-free.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Simulation thread. Called by
		/// <see cref="SimulationThread.WriteSharedState"/> AFTER
		/// <see cref="ExecuteTick"/> returns (sequential within the thread,
		/// so reading physics state maps is safe without an extra lock).
		/// </remarks>
		internal void SnapshotAnimations(ref SimulationState.Snapshot snapshot)
		{
			// --- Balls ---
			var ballCount = 0;
			using (var enumerator = _ctx.BallStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && ballCount < SimulationState.MaxBalls) {
					ref var ball = ref enumerator.Current.Value;
					snapshot.BallSnapshots[ballCount] = new SimulationState.BallSnapshot {
						Id = ball.Id,
						Position = ball.Position,
						Radius = ball.Radius,
						IsFrozen = ball.IsFrozen ? (byte)1 : (byte)0,
						Orientation = ball.BallOrientationForUnity
					};
					ballCount++;
				}
			}
			snapshot.BallCount = ballCount;

			// --- Float animations ---
			var floatCount = 0;

			// Flippers
			using (var enumerator = _ctx.FlipperStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && floatCount < SimulationState.MaxFloatAnimations) {
					ref var s = ref enumerator.Current.Value;
					snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
						ItemId = enumerator.Current.Key, Value = s.Movement.Angle
					};
				}
			}

			// Bumper rings (float) — ring animation
			using (var enumerator = _ctx.BumperStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && floatCount < SimulationState.MaxFloatAnimations) {
					ref var s = ref enumerator.Current.Value;
					if (s.RingItemId != 0) {
						snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
							ItemId = enumerator.Current.Key, Value = s.RingAnimation.Offset
						};
					}
				}
			}

			// Drop targets
			using (var enumerator = _ctx.DropTargetStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && floatCount < SimulationState.MaxFloatAnimations) {
					ref var s = ref enumerator.Current.Value;
					if (s.AnimatedItemId == 0) continue;
					snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
						ItemId = enumerator.Current.Key, Value = s.Animation.ZOffset
					};
				}
			}

			// Hit targets
			using (var enumerator = _ctx.HitTargetStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && floatCount < SimulationState.MaxFloatAnimations) {
					ref var s = ref enumerator.Current.Value;
					if (s.AnimatedItemId == 0) continue;
					snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
						ItemId = enumerator.Current.Key, Value = s.Animation.XRotation
					};
				}
			}

			// Gates
			using (var enumerator = _ctx.GateStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && floatCount < SimulationState.MaxFloatAnimations) {
					ref var s = ref enumerator.Current.Value;
					snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
						ItemId = enumerator.Current.Key, Value = s.Movement.Angle
					};
				}
			}

			// Plungers
			using (var enumerator = _ctx.PlungerStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && floatCount < SimulationState.MaxFloatAnimations) {
					ref var s = ref enumerator.Current.Value;
					snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
						ItemId = enumerator.Current.Key, Value = s.Animation.Position
					};
				}
			}

			// Spinners
			using (var enumerator = _ctx.SpinnerStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && floatCount < SimulationState.MaxFloatAnimations) {
					ref var s = ref enumerator.Current.Value;
					snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
						ItemId = enumerator.Current.Key, Value = s.Movement.Angle
					};
				}
			}

			// Triggers
			using (var enumerator = _ctx.TriggerStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && floatCount < SimulationState.MaxFloatAnimations) {
					ref var s = ref enumerator.Current.Value;
					if (s.AnimatedItemId == 0) continue;
					snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
						ItemId = enumerator.Current.Key, Value = s.Movement.HeightOffset
					};
				}
			}

			snapshot.FloatAnimationCount = floatCount;

			// --- Float2 animations (bumper skirts) ---
			var float2Count = 0;
			using (var enumerator = _ctx.BumperStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && float2Count < SimulationState.MaxFloat2Animations) {
					ref var s = ref enumerator.Current.Value;
					if (s.SkirtItemId != 0) {
						snapshot.Float2Animations[float2Count++] = new SimulationState.Float2Animation {
							ItemId = enumerator.Current.Key, Value = s.SkirtAnimation.Rotation
						};
					}
				}
			}
			snapshot.Float2AnimationCount = float2Count;
		}

		#endregion

		#region Main Thread — Threaded Mode

		// ──────────────────────────────────────────────────────────────
		// Methods in this region execute on the UNITY MAIN THREAD but
		// only when running in simulation-thread mode
		// (UseExternalTiming == true). They consume data produced by
		// the simulation thread.
		// ──────────────────────────────────────────────────────────────

		/// <summary>
		/// Apply physics state to GameObjects. Reads the latest published
		/// snapshot from the triple-buffered <see cref="SimulationState"/>
		/// — completely lock-free.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Main thread only.
		/// </remarks>
		internal void ApplyMovements()
		{
			if (!_ctx.UseExternalTiming || !_ctx.IsInitialized) return;

			if (_ctx.SimulationState == null) {
				throw new InvalidOperationException(
					"ApplyMovements() requires a SimulationState. " +
					"Call SetSimulationState() before enabling external timing.");
			}

			ref readonly var snapshot = ref _ctx.SimulationState.AcquireReadBuffer();
			ApplyMovementsFromSnapshot(in snapshot);
		}

		/// <summary>
		/// Apply visual updates from a triple-buffer snapshot.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Main thread only. Lock-free.<br/>
		/// The single-threaded path uses <see cref="ApplyAllMovements"/> instead.
		/// </remarks>
		private void ApplyMovementsFromSnapshot(in SimulationState.Snapshot snapshot)
		{
			// Balls
			for (var i = 0; i < snapshot.BallCount; i++) {
				var bs = snapshot.BallSnapshots[i];
				if (bs.IsFrozen != 0) continue;
				if (_ctx.BallComponents.TryGetValue(bs.Id, out var ballComponent)) {
					var ballState = new BallState {
						Id = bs.Id,
						Position = bs.Position,
						Radius = bs.Radius,
						IsFrozen = false,
						BallOrientationForUnity = bs.Orientation,
					};
					ballComponent.Move(ballState);
				}
			}

			// Float animations
			for (var i = 0; i < snapshot.FloatAnimationCount; i++) {
				var anim = snapshot.FloatAnimations[i];
				if (_ctx.FloatAnimatedComponents.TryGetValue(anim.ItemId, out var emitter)) {
					emitter.UpdateAnimationValue(anim.Value);
				}
			}

			// Float2 animations
			for (var i = 0; i < snapshot.Float2AnimationCount; i++) {
				var anim = snapshot.Float2Animations[i];
				if (_ctx.Float2AnimatedComponents.TryGetValue(anim.ItemId, out var emitter)) {
					emitter.UpdateAnimationValue(anim.Value);
				}
			}
		}

		/// <summary>
		/// Drain physics-originated managed callbacks on the Unity main thread.
		/// Non-blocking: if the simulation thread currently holds the physics
		/// lock, callbacks are deferred to the next frame.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Main thread only.
		/// </remarks>
		internal void DrainExternalThreadCallbacks()
		{
			if (!_ctx.UseExternalTiming || !_ctx.IsInitialized) {
				return;
			}

			if (!Monitor.TryEnter(_ctx.PhysicsLock)) {
				return; // sim thread is mid-tick; drain next frame
			}
			try {
				while (_ctx.EventQueue.Ref.TryDequeue(out var eventData)) {
					_player.OnEvent(in eventData);
				}

				lock (_ctx.ScheduledActions) {
					for (var i = _ctx.ScheduledActions.Count - 1; i >= 0; i--) {
						if (_ctx.PhysicsEnv.CurPhysicsFrameTime > _ctx.ScheduledActions[i].ScheduleAt) {
							_ctx.ScheduledActions[i].Action();
							_ctx.ScheduledActions.RemoveAt(i);
						}
					}
				}
			} finally {
				Monitor.Exit(_ctx.PhysicsLock);
			}
		}

		/// <summary>
		/// Collect kinematic transform changes on the Unity main thread and
		/// stage them for the sim thread to apply.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Main thread only.<br/>
		/// Uses <see cref="PhysicsEngineContext.MainThreadKinematicCache"/> for
		/// change detection (never reads
		/// <see cref="PhysicsEngineContext.KinematicTransforms"/> which the
		/// sim thread writes). Writes to
		/// <see cref="PhysicsEngineContext.PendingKinematicTransforms"/>
		/// under <see cref="PhysicsEngineContext.PendingKinematicLock"/>.
		/// </remarks>
		internal void UpdateKinematicTransformsFromMainThread()
		{
			if (!_ctx.UseExternalTiming || !_ctx.IsInitialized || _kinematicColliderComponents == null) return;

			foreach (var coll in _kinematicColliderComponents) {
				var currMatrix = coll.GetLocalToPlayfieldMatrixInVpx(_worldToPlayfield);

				// Check against main-thread cache
				if (_ctx.MainThreadKinematicCache.TryGetValue(coll.ItemId, out var lastMatrix) && lastMatrix.Equals(currMatrix)) {
					continue;
				}

				// Transform changed — update cache
				_ctx.MainThreadKinematicCache[coll.ItemId] = currMatrix;

				// Notify the component (e.g. KickerColliderComponent updates its
				// center). NOTE: this writes physics state from the main thread,
				// which is a pre-existing thread-safety issue inherited from the
				// original code. A future improvement would schedule these as
				// input actions.
				coll.OnTransformationChanged(currMatrix);

				// Stage for the sim thread
				lock (_ctx.PendingKinematicLock) {
					_ctx.PendingKinematicTransforms.Ref[coll.ItemId] = currMatrix;
				}
			}
		}

		#endregion

		#region Main Thread — Single-Threaded Mode

		// ──────────────────────────────────────────────────────────────
		// Methods in this region execute on the UNITY MAIN THREAD in
		// single-threaded mode (UseExternalTiming == false).
		// Physics runs and movements apply in the same frame.
		// ──────────────────────────────────────────────────────────────

		/// <summary>
		/// Full physics update including movement application.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Main thread only. Single-threaded mode only.
		/// </remarks>
		internal void ExecutePhysicsUpdate(ulong currentTimeUsec)
		{
			var sw = Stopwatch.StartNew();

			// check for updated kinematic transforms
			_ctx.UpdatedKinematicTransforms.Ref.Clear();
			foreach (var coll in _kinematicColliderComponents) {
				var lastTransformationMatrix = _ctx.KinematicTransforms.Ref[coll.ItemId];
				var currTransformationMatrix = coll.GetLocalToPlayfieldMatrixInVpx(_worldToPlayfield);
				if (lastTransformationMatrix.Equals(currTransformationMatrix)) {
					continue;
				}
				_ctx.UpdatedKinematicTransforms.Ref.Add(coll.ItemId, currTransformationMatrix);
				_ctx.KinematicTransforms.Ref[coll.ItemId] = currTransformationMatrix;
				coll.OnTransformationChanged(currTransformationMatrix);
				_ctx.KinematicOctreeDirty = true;
			}

			var state = _ctx.CreateState();

			// Rebuild kinematic octree only when transforms have changed.
			if (_ctx.KinematicOctreeDirty) {
				PhysicsKinematics.RebuildOctree(ref _ctx.KinematicOctree, ref state);
				_ctx.KinematicOctreeDirty = false;
			}

			// process input
			ProcessInputActions(ref state);

			// run physics loop
			PhysicsUpdate.Execute(
				ref state,
				ref _ctx.PhysicsEnv,
				ref _ctx.OverlappingColliders,
				ref _ctx.KinematicOctree,
				ref _ctx.BallOctree,
				ref _ctx.PhysicsCycle,
				currentTimeUsec
			);

			// dequeue events
			while (_ctx.EventQueue.Ref.TryDequeue(out var eventData)) {
				_player.OnEvent(in eventData);
			}

			// process scheduled events from managed land
			lock (_ctx.ScheduledActions) {
				for (var i = _ctx.ScheduledActions.Count - 1; i >= 0; i--) {
					if (_ctx.PhysicsEnv.CurPhysicsFrameTime > _ctx.ScheduledActions[i].ScheduleAt) {
						_ctx.ScheduledActions[i].Action();
						_ctx.ScheduledActions.RemoveAt(i);
					}
				}
			}

			// Apply movements to GameObjects
			ApplyAllMovements(ref state);

			RecordPhysicsBusyTime(sw.ElapsedTicks);
		}

		/// <summary>
		/// Apply all physics movements to GameObjects directly from state maps.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Main thread only. Single-threaded mode only.<br/>
		/// In threaded mode, <see cref="ApplyMovementsFromSnapshot"/> reads
		/// from the triple-buffered snapshot instead.
		/// </remarks>
		private void ApplyAllMovements(ref PhysicsState state)
		{
			_physicsMovements.ApplyBallMovement(ref state, _ctx.BallComponents);
			_physicsMovements.ApplyFlipperMovement(ref _ctx.FlipperStates.Ref, _ctx.FloatAnimatedComponents);
			_physicsMovements.ApplyBumperMovement(ref _ctx.BumperStates.Ref, _ctx.FloatAnimatedComponents, _ctx.Float2AnimatedComponents);
			_physicsMovements.ApplyDropTargetMovement(ref _ctx.DropTargetStates.Ref, _ctx.FloatAnimatedComponents);
			_physicsMovements.ApplyHitTargetMovement(ref _ctx.HitTargetStates.Ref, _ctx.FloatAnimatedComponents);
			_physicsMovements.ApplyGateMovement(ref _ctx.GateStates.Ref, _ctx.FloatAnimatedComponents);
			_physicsMovements.ApplyPlungerMovement(ref _ctx.PlungerStates.Ref, _ctx.FloatAnimatedComponents);
			_physicsMovements.ApplySpinnerMovement(ref _ctx.SpinnerStates.Ref, _ctx.FloatAnimatedComponents);
			_physicsMovements.ApplyTriggerMovement(ref _ctx.TriggerStates.Ref, _ctx.FloatAnimatedComponents);
		}

		#endregion

		#region Shared

		/// <summary>
		/// Record physics busy time for performance monitoring.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Any (thread-safe via <see cref="Interlocked"/>).
		/// </remarks>
		private void RecordPhysicsBusyTime(long elapsedTicks)
		{
			var elapsedUsec = (elapsedTicks * 1_000_000L) / Stopwatch.Frequency;
			if (elapsedUsec < 0) {
				elapsedUsec = 0;
			}

			Interlocked.Add(ref _ctx.PhysicsBusyTotalUsec, elapsedUsec);
		}

		#endregion
	}
}