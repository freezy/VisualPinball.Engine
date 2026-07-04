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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using NLog;
using Unity.Mathematics;
using VisualPinball.Unity.Collections;
using VisualPinball.Unity.Simulation;
using Logger = NLog.Logger;

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
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly PhysicsEngine _physicsEngine;
		private readonly PhysicsEngineContext _ctx;
		private readonly Player _player;
		private readonly ICollidableComponent[] _kinematicColliderComponents;
		private readonly Dictionary<int, ICollidableComponent> _kinematicColliderComponentsByItemId;
		private readonly float4x4 _worldToPlayfield;
		private readonly PhysicsMovements _physicsMovements = new();
		private readonly List<EventData> _deferredMainThreadEvents = new();
		private readonly List<Action> _deferredMainThreadScheduledActions = new();
		private readonly List<Action> _dueSingleThreadScheduledActions = new();
		private readonly List<PhysicsEngine.InputAction> _pendingInputActions = new();
		private readonly List<KeyValuePair<int, float4x4>> _pendingKinematicUpdates = new();
		private readonly List<int> _pendingKinematicStopUpdates = new();

		/// <summary>
		/// Kinematic items that moved on the last change-detection scan, so a
		/// scan without change can zero their velocity ("stop detection").
		/// Owned by whichever thread runs change detection: the main thread in
		/// threaded mode (<see cref="UpdateKinematicTransformsFromMainThread"/>),
		/// the main thread in single-threaded mode (<see cref="ExecutePhysicsUpdate"/>).
		/// The two modes are mutually exclusive.
		/// </summary>
		private readonly HashSet<int> _movedKinematicItems = new();

		/// <summary>
		/// Large isolated transform jumps held for one-frame disambiguation: a
		/// follow-up update means it was the first frame of continuous motion
		/// (stream it, with derived velocity); a timeout means it was a genuine
		/// teleport (snap silently, no impulse). Owned by the thread that stages
		/// kinematic targets (sim thread, or main thread in single-threaded mode).
		/// </summary>
		private readonly Dictionary<int, HeldKinematicPose> _heldIsolatedPoses = new();
		private readonly List<int> _heldPosesToApply = new();

		private struct HeldKinematicPose
		{
			public float4x4 Pose;
			public ulong HeldAtUsec;
		}
		private readonly int[] _snapshotFlipperIds;
		private readonly int[] _snapshotBumperRingIds;
		private readonly int[] _snapshotBumperSkirtIds;
		private readonly int[] _snapshotDropTargetIds;
		private readonly int[] _snapshotHitTargetIds;
		private readonly int[] _snapshotGateIds;
		private readonly int[] _snapshotPlungerIds;
		private readonly int[] _snapshotSpinnerIds;
		private readonly int[] _snapshotTriggerIds;
		private bool _ballSnapshotOverflowWarningIssued;
		private bool _floatSnapshotOverflowWarningIssued;
		private bool _float2SnapshotOverflowWarningIssued;

		internal PhysicsEngineThreading(PhysicsEngine physicsEngine, PhysicsEngineContext ctx, Player player,
			ICollidableComponent[] kinematicColliderComponents, float4x4 worldToPlayfield)
		{
			_physicsEngine = physicsEngine ?? throw new ArgumentNullException(nameof(physicsEngine));
			_ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
			_player = player;
			_kinematicColliderComponents = kinematicColliderComponents;
			_kinematicColliderComponentsByItemId = new Dictionary<int, ICollidableComponent>(kinematicColliderComponents?.Length ?? 0);
			if (kinematicColliderComponents != null) {
				foreach (var coll in kinematicColliderComponents) {
					_kinematicColliderComponentsByItemId[coll.ItemId] = coll;
				}
			}
			_snapshotFlipperIds = SnapshotIds(_ctx.FlipperStates.Ref);
			_snapshotBumperRingIds = SnapshotIds(_ctx.BumperStates.Ref, static state => state.RingItemId != 0);
			_snapshotBumperSkirtIds = SnapshotIds(_ctx.BumperStates.Ref, static state => state.SkirtItemId != 0);
			_snapshotDropTargetIds = SnapshotIds(_ctx.DropTargetStates.Ref, static state => state.AnimatedItemId != 0);
			_snapshotHitTargetIds = SnapshotIds(_ctx.HitTargetStates.Ref, static state => state.AnimatedItemId != 0);
			_snapshotGateIds = SnapshotIds(_ctx.GateStates.Ref);
			_snapshotPlungerIds = SnapshotIds(_ctx.PlungerStates.Ref);
			_snapshotSpinnerIds = SnapshotIds(_ctx.SpinnerStates.Ref);
			_snapshotTriggerIds = SnapshotIds(_ctx.TriggerStates.Ref, static state => state.AnimatedItemId != 0);
			_worldToPlayfield = worldToPlayfield;
		}

		private static int[] SnapshotIds<TState>(global::Unity.Collections.NativeParallelHashMap<int, TState> map, Func<TState, bool> predicate = null)
			where TState : unmanaged
		{
			var ids = new List<int>();
			using var enumerator = map.GetEnumerator();
			while (enumerator.MoveNext()) {
				if (predicate == null || predicate(enumerator.Current.Value)) {
					ids.Add(enumerator.Current.Key);
				}
			}
			return ids.ToArray();
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
			_physicsEngine.MarkCurrentThreadAsSimulationThread();

			lock (_ctx.PhysicsLock) {
				if (!_ctx.IsInitialized) {
					return;
				}

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
			Interlocked.Exchange(ref _ctx.PublishedPhysicsFrameTimeUsec, (long)_ctx.PhysicsEnv.CurPhysicsFrameTime);

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
			_pendingInputActions.Clear();

			lock (_ctx.InputActionsLock) {
				while (_ctx.InputActions.Count > 0) {
					_pendingInputActions.Add(_ctx.InputActions.Dequeue());
				}
			}

			foreach (var action in _pendingInputActions) {
				action(ref state);
			}
		}

		/// <summary>
		/// Apply kinematic transforms staged by the main thread into the
		/// physics state maps, deriving each item's velocity from the
		/// transform delta. Also drains staged stop events, zeroing the
		/// velocity of items that ceased moving.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Simulation thread (inside <c>PhysicsLock</c>).<br/>
		/// Lock ordering: <c>PhysicsLock</c> (held) then
		/// <c>PendingKinematicLock</c> (inner).<br/>
		/// Stops are processed after transforms: when a final move and its
		/// subsequent stop are drained together, the item must end up with
		/// zero velocity.
		/// </remarks>
		private void ApplyPendingKinematicTransforms()
		{
			if (!_ctx.PendingKinematicTransforms.Ref.IsCreated) return;

			lock (_ctx.PendingKinematicLock) {
				var nowUsec = _ctx.PhysicsEnv.CurPhysicsFrameTime;

				if (_ctx.PendingKinematicTransforms.Ref.Count() > 0) {
					using var enumerator = _ctx.PendingKinematicTransforms.Ref.GetEnumerator();
					while (enumerator.MoveNext()) {
						StageKinematicTarget(enumerator.Current.Key, enumerator.Current.Value, nowUsec);
					}
					_ctx.PendingKinematicTransforms.Ref.Clear();
				}

				if (_ctx.PendingKinematicStops.Count > 0) {
					foreach (var itemId in _ctx.PendingKinematicStops) {
						StopKinematicVelocity(itemId, nowUsec);
					}
					_ctx.PendingKinematicStops.Clear();
				}

				ProcessHeldKinematicPoses(nowUsec);
			}
		}

		/// <summary>
		/// Stage a kinematic transform update: derive the velocity, then either set
		/// the streaming target (continuous motion), snap directly (small isolated
		/// nudge or warp), or hold for one-frame disambiguation (large isolated jump
		/// — could be a teleport or the first frame of a fast drag; see
		/// <see cref="PhysicsKinematics.IsolatedHoldDistance"/>).
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Simulation thread (inside <c>PhysicsLock</c>), or
		/// main thread in single-threaded mode.
		/// </remarks>
		private void StageKinematicTarget(int itemId, in float4x4 matrix, ulong nowUsec)
		{
			var isIsolated = DeriveKinematicVelocity(itemId, in matrix, nowUsec, out var prevMatrix);
			var wasHeld = _heldIsolatedPoses.Remove(itemId); // a follow-up resolves any hold

			if (isIsolated && !wasHeld) {
				if (PhysicsKinematics.IsSmallIsolatedDelta(in prevMatrix, in matrix)) {
					// small isolated nudge / warp: apply as teleport, no impulse
					SnapKinematicPose(itemId, in matrix);
				} else {
					// large isolated jump: hold for disambiguation
					_heldIsolatedPoses[itemId] = new HeldKinematicPose { Pose = matrix, HeldAtUsec = nowUsec };
				}
			} else {
				// continuous motion (incl. resolving a hold): stream toward the target,
				// capped per tick, so a fast collider can't skip past a ball
				_ctx.KinematicTargetTransforms.Ref[itemId] = matrix;
				_ctx.KinematicOctreeDirty = true;
			}

			var coll = GetKinematicColliderComponent(itemId);
			coll?.OnTransformationChanged(matrix);
		}

		/// <summary>
		/// Apply a pose directly with teleport semantics: no stepping, no velocity,
		/// no impulse — the item is just somewhere else now.
		/// </summary>
		private void SnapKinematicPose(int itemId, in float4x4 matrix)
		{
			_ctx.KinematicTransforms.Ref[itemId] = matrix;
			_ctx.KinematicTargetTransforms.Ref[itemId] = matrix; // keep in sync, stepping skips equal poses

			var state = _ctx.CreateState();
			ref var colliderLookups = ref _ctx.KinematicColliderLookups.GetValueByRef(itemId);
			for (var i = 0; i < colliderLookups.Length; i++) {
				state.TransformKinematicColliders(colliderLookups[i], matrix);
			}
			_ctx.KinematicOctreeDirty = true;
		}

		/// <summary>
		/// Apply held isolated poses whose disambiguation window expired without a
		/// follow-up update: they were genuine teleports.
		/// </summary>
		private void ProcessHeldKinematicPoses(ulong nowUsec)
		{
			if (_heldIsolatedPoses.Count == 0) {
				return;
			}
			_heldPosesToApply.Clear();
			foreach (var kvp in _heldIsolatedPoses) {
				if (nowUsec - kvp.Value.HeldAtUsec >= PhysicsKinematics.IsolatedHoldTimeoutUsec) {
					_heldPosesToApply.Add(kvp.Key);
				}
			}
			foreach (var itemId in _heldPosesToApply) {
				SnapKinematicPose(itemId, _heldIsolatedPoses[itemId].Pose);
				_heldIsolatedPoses.Remove(itemId);
			}
		}

		/// <summary>
		/// Derive an item's velocity from its previous and new transform, so
		/// collision resolution can account for the motion of its colliders.
		/// Returns whether the update was isolated (no velocity derived).
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Simulation thread (inside <c>PhysicsLock</c>), or
		/// main thread in single-threaded mode.<br/>
		/// The previous matrix is the held pose if one exists, else the previous
		/// <i>target</i> (the true motion timeline as staged), not the
		/// possibly-lagging stepped pose.
		/// </remarks>
		private bool DeriveKinematicVelocity(int itemId, in float4x4 currMatrix, ulong nowUsec, out float4x4 prevMatrix)
		{
			if (_heldIsolatedPoses.TryGetValue(itemId, out var held)) {
				prevMatrix = held.Pose;
			} else if (!_ctx.KinematicTargetTransforms.Ref.TryGetValue(itemId, out prevMatrix)) {
				prevMatrix = _ctx.KinematicTransforms.Ref[itemId];
			}
			if (_ctx.KinematicVelocities.Ref.TryGetValue(itemId, out var prevVelocity)) {
				_ctx.KinematicVelocities.Ref[itemId] = PhysicsKinematics.DeriveVelocity(in prevVelocity, in prevMatrix, in currMatrix, nowUsec, out var isIsolated);
				return isIsolated;
			}

			// first update: establish a zero-velocity baseline, velocity kicks in with the next update
			_ctx.KinematicVelocities.Ref[itemId] = new KinematicVelocityState {
				Pivot = currMatrix.c3.xyz,
				LastUpdateUsec = nowUsec,
			};
			return true;
		}

		/// <summary>
		/// Zero the velocity of a kinematic item that stopped moving, keeping
		/// its pivot so a later update derives from a valid baseline.
		/// </summary>
		private void StopKinematicVelocity(int itemId, ulong nowUsec)
		{
			if (_ctx.KinematicVelocities.Ref.TryGetValue(itemId, out var velocity)) {
				velocity.LinearVelocity = float3.zero;
				velocity.AngularVelocity = float3.zero;
				velocity.LastUpdateUsec = nowUsec;
				_ctx.KinematicVelocities.Ref[itemId] = velocity;
			}
		}

		private ICollidableComponent GetKinematicColliderComponent(int itemId)
		{
			return _kinematicColliderComponentsByItemId.TryGetValue(itemId, out var coll) ? coll : null;
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
			var ballSourceCount = 0;
			using (var enumerator = _ctx.BallStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ballSourceCount++;
					if (ballCount >= SimulationState.MaxBalls) {
						continue;
					}

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
			snapshot.BallSourceCount = ballSourceCount;
			snapshot.BallSnapshotsTruncated = ballSourceCount > SimulationState.MaxBalls ? (byte)1 : (byte)0;
			if (!_ballSnapshotOverflowWarningIssued && snapshot.BallSnapshotsTruncated != 0) {
				_ballSnapshotOverflowWarningIssued = true;
				Logger.Warn($"[PhysicsEngine] Ball snapshot capacity exceeded: {ballSourceCount} balls for max {SimulationState.MaxBalls}. Snapshot output is truncated.");
			}

			// --- Float animations ---
			var floatCount = 0;
			snapshot.FloatAnimationSourceCount = _snapshotFlipperIds.Length + _snapshotBumperRingIds.Length + _snapshotDropTargetIds.Length + _snapshotHitTargetIds.Length + _snapshotGateIds.Length + _snapshotPlungerIds.Length + _snapshotSpinnerIds.Length + _snapshotTriggerIds.Length;

			// Flippers
			for (var i = 0; i < _snapshotFlipperIds.Length && floatCount < SimulationState.MaxFloatAnimations; i++) {
				var itemId = _snapshotFlipperIds[i];
				ref var s = ref _ctx.FlipperStates.Ref.GetValueByRef(itemId);
				snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
					ItemId = itemId, Value = s.Movement.Angle
				};
			}

			// Bumper rings (float) — ring animation
			for (var i = 0; i < _snapshotBumperRingIds.Length && floatCount < SimulationState.MaxFloatAnimations; i++) {
				var itemId = _snapshotBumperRingIds[i];
				ref var s = ref _ctx.BumperStates.Ref.GetValueByRef(itemId);
				snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
					ItemId = itemId, Value = s.RingAnimation.Offset
				};
			}

			// Drop targets
			for (var i = 0; i < _snapshotDropTargetIds.Length && floatCount < SimulationState.MaxFloatAnimations; i++) {
				var itemId = _snapshotDropTargetIds[i];
				ref var s = ref _ctx.DropTargetStates.Ref.GetValueByRef(itemId);
				snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
					ItemId = itemId, Value = s.Animation.ZOffset
				};
			}

			// Hit targets
			for (var i = 0; i < _snapshotHitTargetIds.Length && floatCount < SimulationState.MaxFloatAnimations; i++) {
				var itemId = _snapshotHitTargetIds[i];
				ref var s = ref _ctx.HitTargetStates.Ref.GetValueByRef(itemId);
				snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
					ItemId = itemId, Value = s.Animation.XRotation
				};
			}

			// Gates
			for (var i = 0; i < _snapshotGateIds.Length && floatCount < SimulationState.MaxFloatAnimations; i++) {
				var itemId = _snapshotGateIds[i];
				ref var s = ref _ctx.GateStates.Ref.GetValueByRef(itemId);
				snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
					ItemId = itemId, Value = s.Movement.Angle
				};
			}

			// Plungers
			for (var i = 0; i < _snapshotPlungerIds.Length && floatCount < SimulationState.MaxFloatAnimations; i++) {
				var itemId = _snapshotPlungerIds[i];
				ref var s = ref _ctx.PlungerStates.Ref.GetValueByRef(itemId);
				snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
					ItemId = itemId, Value = s.Animation.Position
				};
			}

			// Spinners
			for (var i = 0; i < _snapshotSpinnerIds.Length && floatCount < SimulationState.MaxFloatAnimations; i++) {
				var itemId = _snapshotSpinnerIds[i];
				ref var s = ref _ctx.SpinnerStates.Ref.GetValueByRef(itemId);
				snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
					ItemId = itemId, Value = s.Movement.Angle
				};
			}

			// Triggers
			for (var i = 0; i < _snapshotTriggerIds.Length && floatCount < SimulationState.MaxFloatAnimations; i++) {
				var itemId = _snapshotTriggerIds[i];
				ref var s = ref _ctx.TriggerStates.Ref.GetValueByRef(itemId);
				snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
					ItemId = itemId, Value = s.Movement.HeightOffset
				};
			}

			snapshot.FloatAnimationCount = floatCount;
			snapshot.FloatAnimationsTruncated = snapshot.FloatAnimationSourceCount > SimulationState.MaxFloatAnimations ? (byte)1 : (byte)0;
			if (!_floatSnapshotOverflowWarningIssued && snapshot.FloatAnimationsTruncated != 0) {
				_floatSnapshotOverflowWarningIssued = true;
				Logger.Warn($"[PhysicsEngine] Float animation snapshot capacity exceeded: {snapshot.FloatAnimationSourceCount} channels for max {SimulationState.MaxFloatAnimations}. Snapshot output is truncated.");
			}

			// --- Float2 animations (bumper skirts) ---
			var float2Count = 0;
			snapshot.Float2AnimationSourceCount = _snapshotBumperSkirtIds.Length;
			for (var i = 0; i < _snapshotBumperSkirtIds.Length && float2Count < SimulationState.MaxFloat2Animations; i++) {
				var itemId = _snapshotBumperSkirtIds[i];
				ref var s = ref _ctx.BumperStates.Ref.GetValueByRef(itemId);
				snapshot.Float2Animations[float2Count++] = new SimulationState.Float2Animation {
					ItemId = itemId, Value = s.SkirtAnimation.Rotation
				};
			}
			snapshot.Float2AnimationCount = float2Count;
			snapshot.Float2AnimationsTruncated = snapshot.Float2AnimationSourceCount > SimulationState.MaxFloat2Animations ? (byte)1 : (byte)0;
			if (!_float2SnapshotOverflowWarningIssued && snapshot.Float2AnimationsTruncated != 0) {
				_float2SnapshotOverflowWarningIssued = true;
				Logger.Warn($"[PhysicsEngine] Float2 animation snapshot capacity exceeded: {snapshot.Float2AnimationSourceCount} channels for max {SimulationState.MaxFloat2Animations}. Snapshot output is truncated.");
			}
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

			_deferredMainThreadEvents.Clear();
			_deferredMainThreadScheduledActions.Clear();
			var drainStartTicks = Stopwatch.GetTimestamp();

			if (!Monitor.TryEnter(_ctx.PhysicsLock)) {
				return; // sim thread is mid-tick; drain next frame
			}
			try {
				while (_ctx.EventQueue.Ref.TryDequeue(out var eventData)) {
					_deferredMainThreadEvents.Add(eventData);
				}

				lock (_ctx.ScheduledActionsLock) {
					DrainDueScheduledActions(_ctx.PhysicsEnv.CurPhysicsFrameTime, _deferredMainThreadScheduledActions);
				}
			} finally {
				Monitor.Exit(_ctx.PhysicsLock);
			}

			foreach (var eventData in _deferredMainThreadEvents) {
				_player.OnEvent(in eventData);
			}

			foreach (var action in _deferredMainThreadScheduledActions) {
				action();
			}

			Interlocked.Exchange(ref _ctx.LastEventDrainUsec, ElapsedUsec(drainStartTicks, Stopwatch.GetTimestamp()));
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

			var scanStartTicks = Stopwatch.GetTimestamp();

			_pendingKinematicUpdates.Clear();
			_pendingKinematicStopUpdates.Clear();

			foreach (var coll in _kinematicColliderComponents) {
				var currMatrix = coll.GetLocalToPlayfieldMatrixInVpx(_worldToPlayfield);

				// Check against main-thread cache
				if (_ctx.MainThreadKinematicCache.TryGetValue(coll.ItemId, out var lastMatrix) && lastMatrix.Equals(currMatrix)) {
					// unchanged — if it moved last frame, it just stopped, so stage a velocity reset
					if (_movedKinematicItems.Remove(coll.ItemId)) {
						_pendingKinematicStopUpdates.Add(coll.ItemId);
					}
					continue;
				}

				// Transform changed — update cache
				_ctx.MainThreadKinematicCache[coll.ItemId] = currMatrix;
				_movedKinematicItems.Add(coll.ItemId);
				_pendingKinematicUpdates.Add(new KeyValuePair<int, float4x4>(coll.ItemId, currMatrix));
			}

			if (_pendingKinematicUpdates.Count == 0 && _pendingKinematicStopUpdates.Count == 0) {
				return;
			}

			lock (_ctx.PendingKinematicLock) {
				foreach (var update in _pendingKinematicUpdates) {
					_ctx.PendingKinematicTransforms.Ref[update.Key] = update.Value;
				}
				foreach (var itemId in _pendingKinematicStopUpdates) {
					_ctx.PendingKinematicStops.Add(itemId);
				}
			}

			Interlocked.Exchange(ref _ctx.LastKinematicScanUsec, ElapsedUsec(scanStartTicks, Stopwatch.GetTimestamp()));
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

			// check for updated kinematic transforms; compare against the last staged
			// pose — held or target — not the stepped current pose, which may lag
			// behind while catching up
			foreach (var coll in _kinematicColliderComponents) {
				float4x4 lastTransformationMatrix;
				if (_heldIsolatedPoses.TryGetValue(coll.ItemId, out var held)) {
					lastTransformationMatrix = held.Pose;
				} else if (!_ctx.KinematicTargetTransforms.Ref.TryGetValue(coll.ItemId, out lastTransformationMatrix)) {
					lastTransformationMatrix = _ctx.KinematicTransforms.Ref[coll.ItemId];
				}
				var currTransformationMatrix = coll.GetLocalToPlayfieldMatrixInVpx(_worldToPlayfield);
				if (lastTransformationMatrix.Equals(currTransformationMatrix)) {
					// unchanged — if it moved last frame, it just stopped, so zero its velocity
					if (_movedKinematicItems.Remove(coll.ItemId)) {
						StopKinematicVelocity(coll.ItemId, _ctx.PhysicsEnv.CurPhysicsFrameTime);
					}
					continue;
				}
				_movedKinematicItems.Add(coll.ItemId);
				StageKinematicTarget(coll.ItemId, in currTransformationMatrix, _ctx.PhysicsEnv.CurPhysicsFrameTime);
			}
			ProcessHeldKinematicPoses(_ctx.PhysicsEnv.CurPhysicsFrameTime);

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
			Interlocked.Exchange(ref _ctx.PublishedPhysicsFrameTimeUsec, (long)_ctx.PhysicsEnv.CurPhysicsFrameTime);

			// dequeue events
			while (_ctx.EventQueue.Ref.TryDequeue(out var eventData)) {
				_player.OnEvent(in eventData);
			}

			_dueSingleThreadScheduledActions.Clear();
			lock (_ctx.ScheduledActionsLock) {
				DrainDueScheduledActions(_ctx.PhysicsEnv.CurPhysicsFrameTime, _dueSingleThreadScheduledActions);
			}
			foreach (var action in _dueSingleThreadScheduledActions) {
				action();
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

		private static long ElapsedUsec(long startTicks, long endTicks)
		{
			var elapsedTicks = endTicks - startTicks;
			if (elapsedTicks < 0) {
				elapsedTicks = 0;
			}
			return (elapsedTicks * 1_000_000L) / Stopwatch.Frequency;
		}

		private void DrainDueScheduledActions(ulong currentTimeUsec, List<Action> destination)
		{
			while (_ctx.ScheduledActions.Count > 0 && _ctx.ScheduledActions[0].ScheduleAt < currentTimeUsec) {
				destination.Add(PopScheduledAction().Action);
			}
		}

		private PhysicsEngineContext.ScheduledAction PopScheduledAction()
		{
			var scheduledActions = _ctx.ScheduledActions;
			var root = scheduledActions[0];
			var lastIndex = scheduledActions.Count - 1;
			var last = scheduledActions[lastIndex];
			scheduledActions.RemoveAt(lastIndex);

			if (lastIndex == 0) {
				return root;
			}

			scheduledActions[0] = last;
			var index = 0;
			while (true) {
				var left = index * 2 + 1;
				if (left >= scheduledActions.Count) {
					break;
				}

				var right = left + 1;
				var smallest = right < scheduledActions.Count && scheduledActions[right].ScheduleAt < scheduledActions[left].ScheduleAt
					? right
					: left;

				if (scheduledActions[index].ScheduleAt <= scheduledActions[smallest].ScheduleAt) {
					break;
				}

				(scheduledActions[index], scheduledActions[smallest]) = (scheduledActions[smallest], scheduledActions[index]);
				index = smallest;
			}

			return root;
		}

		#endregion
	}
}
