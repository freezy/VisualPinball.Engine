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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NativeTrees;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Collections;
using VisualPinball.Unity.Simulation;
using AABB = NativeTrees.AABB;
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Central physics engine for the Visual Pinball simulation.
	///
	/// <para><b>Operating Modes</b></para>
	/// <list type="bullet">
	///   <item><b>Single-threaded</b> (default): <see cref="Update"/> runs
	///     physics and applies movements on the Unity main thread via
	///     <c>ExecutePhysicsUpdate</c>.</item>
	///   <item><b>Simulation-thread</b>: A dedicated 1 kHz thread calls
	///     <see cref="ExecuteTick"/>. The main thread reads the latest
	///     snapshot lock-free via <see cref="PhysicsEngineThreading.ApplyMovements"/>
	///     and the triple-buffered <see cref="SimulationState"/>.</item>
	/// </list>
	///
	/// <para><b>Architecture</b></para>
	/// <para>This MonoBehaviour is a thin lifecycle/API shell. All shared
	/// mutable state lives in <see cref="PhysicsEngineContext"/> (created
	/// eagerly as a field initializer, populated in <c>Awake</c>/<c>Start</c>,
	/// disposed in <c>OnDestroy</c>).
	/// All threading/tick methods live in
	/// <see cref="PhysicsEngineThreading"/> (created at end of
	/// <c>Start</c>).</para>
	///
	/// <para><b>Threading Contract</b></para>
	/// <para>Four threads participate at runtime:</para>
	/// <list type="number">
	///   <item><b>Unity main thread</b> (~60-144 Hz) — rendering, UI, event
	///     drain, visual state application.</item>
	///   <item><b>Simulation thread</b> (1000 Hz) — physics ticks, input
	///     dispatch, coil output processing, GLE time fence.</item>
	///   <item><b>Native input polling thread</b> (500-2000 Hz) — raw
	///     keyboard/gamepad polling via the <c>VisualPinball.NativeInput</c> native plugin.</item>
	///   <item><b>PinMAME emulation thread</b> (variable, time-fenced) —
	///     ROM emulation.</item>
	/// </list>
	///
	/// <para><b>Communication Channels</b></para>
	/// <list type="table">
	///   <listheader>
	///     <term>Channel</term>
	///     <description>Direction / Protection</description>
	///   </listheader>
	///   <item>
	///     <term><see cref="SimulationState"/> (triple buffer)</term>
	///     <description>Sim -> Main. Lock-free (atomic index swap).
	///     THE canonical channel for animation data (ball positions,
	///     flipper angles, etc.).</description>
	///   </item>
	///   <item>
	///     <term><c>PendingKinematicTransforms</c></term>
	///     <description>Main -> Sim. Protected by
	///     <c>PendingKinematicLock</c>.</description>
	///   </item>
	///   <item>
	///     <term><c>InputActions</c> queue</term>
	///     <description>Any -> Sim. Protected by
	///     <c>InputActionsLock</c>. Used by component APIs
	///     (<c>Schedule</c>) to enqueue state mutations.</description>
	///   </item>
	///   <item>
	///     <term><c>EventQueue</c></term>
	///     <description>Sim -> Main. NativeQueue; drained under
	///     <c>PhysicsLock</c> via <c>Monitor.TryEnter</c>.</description>
	///   </item>
	///   <item>
	///     <term><c>PhysicsLock</c></term>
	///     <description>Coarse lock held by sim thread during tick. Main
	///     thread acquires non-blockingly for callback drain.</description>
	///   </item>
	/// </list>
	///
	/// <para><b>Lock Ordering</b></para>
	/// <para><c>PhysicsLock</c> -> <c>PendingKinematicLock</c>
	/// -> <c>InputActionsLock</c></para>
	/// </summary>
	[PackAs("PhysicsEngine")]
	public class PhysicsEngine : MonoBehaviour, IPackable
	{
		#region Configuration

		[Tooltip("Gravity constant, in VPX units.")]
		public float GravityStrength = PhysicsConstants.GravityConst * PhysicsConstants.DefaultTableGravity;

		#endregion

		#region Packaging

		public byte[] Pack() => PhysicsEnginePackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) => PhysicsEnginePackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion

		#region Fields

		/// <summary>
		/// All shared mutable state. Initialized eagerly as a field
		/// initializer so that other components' <c>Awake</c> / <c>OnEnable</c>
		/// can safely call <c>Register</c> / <c>EnableCollider</c> before
		/// this MonoBehaviour's own <c>Awake</c> runs (Unity does not
		/// guarantee <c>Awake</c> order between sibling components).
		/// Populated through <see cref="Awake"/> and <see cref="Start"/>,
		/// disposed in <see cref="OnDestroy"/>.
		/// </summary>
		[NonSerialized] private readonly PhysicsEngineContext _ctx = new();

		/// <summary>
		/// Threading/tick methods. Created at end of <see cref="Start"/>
		/// once all context fields are populated.
		/// </summary>
		[NonSerialized] private PhysicsEngineThreading _threading;

		// Lifecycle-local references (used during Awake/Start, then passed
		// to _threading constructor).
		[NonSerialized] private Player _player;
		[NonSerialized] private ICollidableComponent[] _colliderComponents;
		[NonSerialized] private ICollidableComponent[] _kinematicColliderComponents;
		[NonSerialized] private float4x4 _worldToPlayfield;
		[NonSerialized] private int _mainThreadManagedThreadId;
		[NonSerialized] private int _simulationThreadManagedThreadId = -1;
		[NonSerialized] private readonly HashSet<string> _unsafeLiveStateAccessWarnings = new HashSet<string>();
		[NonSerialized] private bool _inputActionsQueueWarningIssued;
		[NonSerialized] private bool _scheduledActionsQueueWarningIssued;

		private const int InputActionsQueueWarningThreshold = 256;
		private const int ScheduledActionsWarningThreshold = 256;

		#endregion

		private static ulong NowUsec => (ulong)(Time.timeAsDouble * 1000000);
		internal ulong CurrentSimulationClockUsec => NowUsec;
		internal float CurrentSimulationClockScale => Time.timeScale;
		internal bool UsesExternalTiming => _ctx.UseExternalTiming;

		/// <summary>
		/// Check if the physics engine has completed initialization.
		/// Used by the simulation thread to wait for physics to be ready.
		/// </summary>
		public bool IsInitialized => _ctx != null && _ctx.IsInitialized;

		#region Ref-Return Properties

		/// <summary>
		/// Elasticity-over-velocity lookup tables, keyed by item ID.
		/// </summary>
		/// <remarks>
		/// Accessed by <see cref="CollidableApi"/> via <c>ref</c> return
		/// (see <c>CollidableApi.cs:68</c>). Must remain a ref-return
		/// property so callers can take a reference to the native map.
		/// </remarks>
		public ref NativeParallelHashMap<int, FixedList512Bytes<float>> ElasticityOverVelocityLUTs => ref _ctx.ElasticityOverVelocityLUTs;

		/// <summary>
		/// Friction-over-velocity lookup tables, keyed by item ID.
		/// </summary>
		/// <remarks>
		/// Same ref-return requirement as
		/// <see cref="ElasticityOverVelocityLUTs"/>.
		/// </remarks>
		public ref NativeParallelHashMap<int, FixedList512Bytes<float>> FrictionOverVelocityLUTs => ref _ctx.FrictionOverVelocityLUTs;

		#endregion

		#region API

		public void ScheduleAction(int timeoutMs, Action action) => ScheduleAction((uint)timeoutMs, action);
		public void ScheduleAction(uint timeoutMs, Action action)
		{
			var scheduleAt = CurrentScheduledActionTimeUsec + (ulong)timeoutMs * 1000;
			lock (_ctx.ScheduledActionsLock) {
				PushScheduledAction(new PhysicsEngineContext.ScheduledAction(scheduleAt, action));
				if (!_scheduledActionsQueueWarningIssued && _ctx.ScheduledActions.Count >= ScheduledActionsWarningThreshold) {
					_scheduledActionsQueueWarningIssued = true;
					LogQueueWarning($"[PhysicsEngine] ScheduledActions backlog reached {_ctx.ScheduledActions.Count} items. Callback production may be outpacing drain.");
				}
			}
		}

		private void PushScheduledAction(PhysicsEngineContext.ScheduledAction action)
		{
			var scheduledActions = _ctx.ScheduledActions;
			scheduledActions.Add(action);
			var index = scheduledActions.Count - 1;
			while (index > 0) {
				var parentIndex = (index - 1) / 2;
				if (scheduledActions[parentIndex].ScheduleAt <= scheduledActions[index].ScheduleAt) {
					break;
				}

				(scheduledActions[parentIndex], scheduledActions[index]) = (scheduledActions[index], scheduledActions[parentIndex]);
				index = parentIndex;
			}
		}

		internal delegate void InputAction(ref PhysicsState state);

		internal ref NativeParallelHashMap<int, BallState> Balls => ref _ctx.BallStates.Ref;
		internal ref InsideOfs InsideOfs => ref _ctx.InsideOfs;
		internal NativeQueue<EventData>.ParallelWriter EventQueue => _ctx.EventQueue.Ref.AsParallelWriter();

		/// <summary>
		/// Enqueue a state mutation to be processed on the sim thread
		/// (or main thread in single-threaded mode) inside <c>PhysicsLock</c>.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Any thread. Protected by <c>InputActionsLock</c>.
		/// </remarks>
		internal void Schedule(InputAction action)
		{
			lock (_ctx.InputActionsLock) {
				_ctx.InputActions.Enqueue(action);
				if (!_inputActionsQueueWarningIssued && _ctx.InputActions.Count >= InputActionsQueueWarningThreshold) {
					_inputActionsQueueWarningIssued = true;
					LogQueueWarning($"[PhysicsEngine] InputActions backlog reached {_ctx.InputActions.Count} items. Producers may be outpacing simulation-thread drain.");
				}
			}
		}

		internal void MutateState(InputAction action)
		{
			if (_ctx.UseExternalTiming) {
				Schedule(action);
				return;
			}

			var state = _ctx.CreateState();
			action(ref state);
		}

		internal void MarkCurrentThreadAsSimulationThread()
		{
			Interlocked.Exchange(ref _simulationThreadManagedThreadId, Thread.CurrentThread.ManagedThreadId);
		}

		private bool IsMainThread => _mainThreadManagedThreadId == Thread.CurrentThread.ManagedThreadId;
		private bool IsSimulationThread => Volatile.Read(ref _simulationThreadManagedThreadId) == Thread.CurrentThread.ManagedThreadId;
		private ulong CurrentScheduledActionTimeUsec
		{
			get {
				if (!_ctx.UseExternalTiming || IsSimulationThread) {
					return _ctx.PhysicsEnv.CurPhysicsFrameTime;
				}

				var publishedUsec = Interlocked.Read(ref _ctx.PublishedPhysicsFrameTimeUsec);
				return publishedUsec > 0 ? (ulong)publishedUsec : CurrentSimulationClockUsec;
			}
		}

		private void GuardLiveStateAccess(string accessorName)
		{
			if (!_ctx.UseExternalTiming || IsSimulationThread) {
				return;
			}

			if (!IsMainThread) {
				throw new InvalidOperationException($"Live physics state '{accessorName}' accessed from unsupported thread {Thread.CurrentThread.ManagedThreadId}. In external-timing mode, live state is owned by the simulation thread.");
			}

			if (_unsafeLiveStateAccessWarnings.Add(accessorName)) {
				LogQueueWarning($"[PhysicsEngine] Live physics state '{accessorName}' was accessed from the Unity main thread while external timing is enabled. Prefer snapshot data or schedule work onto the simulation thread.");
			}
		}

		[System.Diagnostics.Conditional("UNITY_EDITOR")]
		[System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
		private static void LogQueueWarning(string message)
		{
			Debug.LogWarning(message);
		}

		// ── State accessors ──────────────────────────────────────────
		// These return refs into native hash maps. In single-threaded
		// mode they are safe. In threaded mode, callers on the sim
		// thread (e.g. FlipperApi coil callbacks) access them inside
		// PhysicsLock. Main-thread callers should only read through
		// the triple-buffered snapshot; direct access is a pre-existing
		// thread-safety concern (see AGENTS.md audit notes).

		internal bool BallExists(int ballId)
		{
			GuardLiveStateAccess(nameof(BallExists));
			return _ctx.BallStates.Ref.ContainsKey(ballId);
		}
		internal ref BallState BallState(int ballId)
		{
			GuardLiveStateAccess(nameof(BallState));
			return ref _ctx.BallStates.Ref.GetValueByRef(ballId);
		}
		internal ref BumperState BumperState(int itemId)
		{
			GuardLiveStateAccess(nameof(BumperState));
			return ref _ctx.BumperStates.Ref.GetValueByRef(itemId);
		}
		internal ref FlipperState FlipperState(int itemId)
		{
			GuardLiveStateAccess(nameof(FlipperState));
			return ref _ctx.FlipperStates.Ref.GetValueByRef(itemId);
		}
		internal ref GateState GateState(int itemId)
		{
			GuardLiveStateAccess(nameof(GateState));
			return ref _ctx.GateStates.Ref.GetValueByRef(itemId);
		}
		internal ref DropTargetState DropTargetState(int itemId)
		{
			GuardLiveStateAccess(nameof(DropTargetState));
			return ref _ctx.DropTargetStates.Ref.GetValueByRef(itemId);
		}
		internal ref HitTargetState HitTargetState(int itemId)
		{
			GuardLiveStateAccess(nameof(HitTargetState));
			return ref _ctx.HitTargetStates.Ref.GetValueByRef(itemId);
		}
		internal ref KickerState KickerState(int itemId)
		{
			GuardLiveStateAccess(nameof(KickerState));
			return ref _ctx.KickerStates.Ref.GetValueByRef(itemId);
		}
		internal ref PlungerState PlungerState(int itemId)
		{
			GuardLiveStateAccess(nameof(PlungerState));
			return ref _ctx.PlungerStates.Ref.GetValueByRef(itemId);
		}
		internal ref SpinnerState SpinnerState(int itemId)
		{
			GuardLiveStateAccess(nameof(SpinnerState));
			return ref _ctx.SpinnerStates.Ref.GetValueByRef(itemId);
		}
		internal ref SurfaceState SurfaceState(int itemId)
		{
			GuardLiveStateAccess(nameof(SurfaceState));
			return ref _ctx.SurfaceStates.Ref.GetValueByRef(itemId);
		}
		internal ref TriggerState TriggerState(int itemId)
		{
			GuardLiveStateAccess(nameof(TriggerState));
			return ref _ctx.TriggerStates.Ref.GetValueByRef(itemId);
		}
		internal void SetBallInsideOf(int ballId, int itemId)
		{
			GuardLiveStateAccess(nameof(SetBallInsideOf));
			_ctx.InsideOfs.SetInsideOf(itemId, ballId);
		}
		internal bool HasBallsInsideOf(int itemId)
		{
			GuardLiveStateAccess(nameof(HasBallsInsideOf));
			return _ctx.InsideOfs.GetInsideCount(itemId) > 0;
		}
		internal FixedList64Bytes<int> GetBallsInsideOf(int itemId)
		{
			GuardLiveStateAccess(nameof(GetBallsInsideOf));
			return _ctx.InsideOfs.GetIdsOfBallsInsideItem(itemId);
		}

		internal uint TimeMsec => _ctx.PhysicsEnv.TimeMsec;
		internal Random Random => _ctx.PhysicsEnv.Random;
		internal void Register<T>(T item) where T : MonoBehaviour
		{
			var go = item.gameObject;
			var itemId = go.GetInstanceID();

			// states
			switch (item) {
				case BallComponent c:
					if (!_ctx.BallStates.Ref.ContainsKey(itemId)) {
						_ctx.BallStates.Ref[itemId] = c.CreateState();
					}
					_ctx.BallComponents.TryAdd(itemId, c);
					break;
				case BumperComponent c: _ctx.BumperStates.Ref[itemId] = c.CreateState(); break;
				case FlipperComponent c:
					_ctx.FlipperStates.Ref[itemId] = c.CreateState();
					break;
				case GateComponent c: _ctx.GateStates.Ref[itemId] = c.CreateState(); break;
				case DropTargetComponent c: _ctx.DropTargetStates.Ref[itemId] = c.CreateState(); break;
				case HitTargetComponent c: _ctx.HitTargetStates.Ref[itemId] = c.CreateState(); break;
				case KickerComponent c: _ctx.KickerStates.Ref[itemId] = c.CreateState(); break;
				case PlungerComponent c:
					_ctx.PlungerStates.Ref[itemId] = c.CreateState();
					break;
				case SpinnerComponent c: _ctx.SpinnerStates.Ref[itemId] = c.CreateState(); break;
				case SurfaceComponent c: _ctx.SurfaceStates.Ref[itemId] = c.CreateState(); break;
				case TriggerComponent c: _ctx.TriggerStates.Ref[itemId] = c.CreateState(); break;
			}

			// animations
			if (item is IAnimationValueEmitter<float> floatAnimatedComponent) {
				_ctx.FloatAnimatedComponents.TryAdd(itemId, floatAnimatedComponent);
			}
			if (item is IAnimationValueEmitter<float2> float2AnimatedComponent) {
				_ctx.Float2AnimatedComponents.TryAdd(itemId, float2AnimatedComponent);
			}
		}

		internal BallComponent UnregisterBall(int ballId)
		{
			if (_ctx.UseExternalTiming) {
				throw new InvalidOperationException("Use UnregisterRuntimeBall() when external timing is enabled.");
			}

			var b = _ctx.BallComponents[ballId];
			_ctx.BallComponents.Remove(ballId);
			_ctx.BallStates.Ref.Remove(ballId);
			_ctx.InsideOfs.SetOutsideOfAll(ballId);
			return b;
		}

		internal void RegisterRuntimeBall(BallComponent ball)
		{
			var ballId = ball.gameObject.GetInstanceID();
			var ballState = ball.CreateState();
			_ctx.BallComponents[ballId] = ball;

			if (_ctx.UseExternalTiming) {
				lock (_ctx.PhysicsLock) {
					if (_ctx.BallStates.Ref.IsCreated && !_ctx.BallStates.Ref.ContainsKey(ballId)) {
						_ctx.BallStates.Ref[ballId] = ballState;
					}
				}
				return;
			}

			if (!_ctx.BallStates.Ref.ContainsKey(ballId)) {
				_ctx.BallStates.Ref[ballId] = ballState;
			}
		}

		internal BallComponent UnregisterRuntimeBall(int ballId)
		{
			var ball = _ctx.BallComponents[ballId];
			_ctx.BallComponents.Remove(ballId);

			if (_ctx.UseExternalTiming) {
				lock (_ctx.PhysicsLock) {
					if (_ctx.BallStates.Ref.IsCreated) {
						_ctx.BallStates.Ref.Remove(ballId);
					}
					_ctx.InsideOfs.SetOutsideOfAll(ballId);
				}
				return ball;
			}

			_ctx.BallStates.Ref.Remove(ballId);
			_ctx.InsideOfs.SetOutsideOfAll(ballId);

			return ball;
		}

		internal bool IsColliderEnabled(int itemId) => !_ctx.DisabledCollisionItems.Ref.Contains(itemId);

		internal void EnableCollider(int itemId)
		{
			MutateState((ref PhysicsState state) => state.EnableColliders(itemId));
		}
		internal void DisableCollider(int itemId)
		{
			MutateState((ref PhysicsState state) => state.DisableColliders(itemId));
		}

		public BallComponent GetBall(int itemId) => _ctx.BallComponents[itemId];

		#endregion

		#region Forwarding — Simulation Thread

		/// <summary>
		/// Execute a single physics tick with external timing.
		/// Forwarded to <see cref="PhysicsEngineThreading.ExecuteTick"/>.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Simulation thread (called by
		/// <see cref="SimulationThread"/>).
		/// </remarks>
		public void ExecuteTick(ulong timeUsec) => _threading.ExecuteTick(timeUsec);

		/// <summary>
		/// Copy current animation values into a snapshot buffer.
		/// Forwarded to <see cref="PhysicsEngineThreading.SnapshotAnimations"/>.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Simulation thread.
		/// </remarks>
		internal void SnapshotAnimations(ref SimulationState.Snapshot snapshot) => _threading.SnapshotAnimations(ref snapshot);

		internal bool TrySnapshotAnimations(ref SimulationState.Snapshot snapshot)
		{
			if (!_ctx.UseExternalTiming || !_ctx.IsInitialized) {
				return false;
			}

			lock (_ctx.PhysicsLock) {
				if (!_ctx.IsInitialized) {
					return false;
				}

				_threading.SnapshotAnimations(ref snapshot);
				return true;
			}
		}

		internal void FillDiagnostics(ref SimulationState.Snapshot snapshot)
		{
			snapshot.KinematicScanUsec = Interlocked.Read(ref _ctx.LastKinematicScanUsec);
			snapshot.EventDrainUsec = Interlocked.Read(ref _ctx.LastEventDrainUsec);

			lock (_ctx.InputActionsLock) {
				snapshot.PendingInputActionCount = _ctx.InputActions.Count;
			}

			lock (_ctx.ScheduledActionsLock) {
				snapshot.PendingScheduledActionCount = _ctx.ScheduledActions.Count;
			}
		}

		#endregion

		#region Event Functions

		private void Awake()
		{
			_mainThreadManagedThreadId = Thread.CurrentThread.ManagedThreadId;
			_player = GetComponentInParent<Player>();
			_ctx.InsideOfs = new InsideOfs(Allocator.Persistent);
			_ctx.PhysicsEnv = new PhysicsEnv(NowUsec, GetComponentInChildren<PlayfieldComponent>(), GravityStrength);
			Interlocked.Exchange(ref _ctx.PublishedPhysicsFrameTimeUsec, (long)_ctx.PhysicsEnv.CurPhysicsFrameTime);
			_ctx.ElasticityOverVelocityLUTs = new NativeParallelHashMap<int, FixedList512Bytes<float>>(0, Allocator.Persistent);
			_ctx.FrictionOverVelocityLUTs = new NativeParallelHashMap<int, FixedList512Bytes<float>>(0, Allocator.Persistent);

			_colliderComponents = GetComponentsInChildren<ICollidableComponent>();
			_kinematicColliderComponents = _colliderComponents.Where(c => c.IsKinematic).ToArray();
		}

		private void Start()
		{
			var sw = Stopwatch.StartNew();
			var playfield = GetComponentInChildren<PlayfieldComponent>();

			// register frame pacing stats
			var stats = FindFirstObjectByType<FramePacingGraph>();
			if (stats) {
				long lastBusyTotalUsec = Interlocked.Read(ref _ctx.PhysicsBusyTotalUsec);
				InputLatencyTracker.Reset();
				stats.RegisterCustomMetric("Physics", Color.magenta, () => {
					var totalBusyUsec = Interlocked.Read(ref _ctx.PhysicsBusyTotalUsec);
					var deltaBusyUsec = totalBusyUsec - lastBusyTotalUsec;
					if (deltaBusyUsec < 0) {
						deltaBusyUsec = 0;
					}
					lastBusyTotalUsec = totalBusyUsec;
					return deltaBusyUsec / 1000f;
				});
				stats.RegisterCustomMetric("In Lat (ms)", new Color(0.65f, 1f, 0.3f, 0.9f), InputLatencyTracker.SampleFlipperLatencyMs);
			}

			// create static octree
			Debug.Log($"Found {_colliderComponents.Length} collidable items ({_kinematicColliderComponents.Length} kinematic).");
			var colliders = new ColliderReference(ref _ctx.NonTransformableColliderTransforms.Ref, Allocator.Temp);
			var kinematicColliders = new ColliderReference(ref _ctx.NonTransformableColliderTransforms.Ref, Allocator.Temp, true);
			foreach (var colliderItem in _colliderComponents) {
				if (!colliderItem.IsCollidable) {
					_ctx.DisabledCollisionItems.Ref.Add(colliderItem.ItemId);
				}

				var translateWithinPlayfieldMatrix = colliderItem.GetLocalToPlayfieldMatrixInVpx(playfield.transform.worldToLocalMatrix);
				// todo check if we cannot only add those that are actually non-transformable
				_ctx.NonTransformableColliderTransforms.Ref[colliderItem.ItemId] = translateWithinPlayfieldMatrix;

				if (colliderItem.IsKinematic) {
					colliderItem.GetColliders(_player, this, ref kinematicColliders, translateWithinPlayfieldMatrix, 0);
				} else {
					colliderItem.GetColliders(_player, this, ref colliders, translateWithinPlayfieldMatrix, 0);
				}
			}

			// allocate colliders
			_ctx.Colliders = new NativeColliders(ref colliders, Allocator.Persistent);
			_ctx.KinematicColliders = new NativeColliders(ref kinematicColliders, Allocator.Persistent);

			// get kinetic collider matrices
			_worldToPlayfield = playfield.transform.worldToLocalMatrix;
			foreach (var coll in _kinematicColliderComponents) {
				var matrix = coll.GetLocalToPlayfieldMatrixInVpx(_worldToPlayfield);
				_ctx.KinematicTransforms.Ref[coll.ItemId] = matrix;
				_ctx.MainThreadKinematicCache[coll.ItemId] = matrix;
			}
#if UNITY_EDITOR
			_ctx.ColliderLookups = colliders.CreateLookup(Allocator.Persistent);
#endif
			_ctx.KinematicColliderLookups = kinematicColliders.CreateLookup(Allocator.Persistent);

			// create identity kinematic colliders
			kinematicColliders.TransformToIdentity(ref _ctx.KinematicTransforms.Ref);
			_ctx.KinematicCollidersAtIdentity = new NativeColliders(ref kinematicColliders, Allocator.Persistent);

			// create octree
			var elapsedMs = sw.Elapsed.TotalMilliseconds;
			_ctx.PlayfieldBounds = playfield.Bounds;
			_ctx.Octree = new NativeOctree<int>(_ctx.PlayfieldBounds, 1024, 10, Allocator.Persistent);

			sw.Restart();
			unsafe {
				fixed (NativeColliders* c = &_ctx.Colliders)
				fixed (NativeOctree<int>* o = &_ctx.Octree) {
					PhysicsPopulate.PopulateUnsafe((IntPtr)c, (IntPtr)o);
				}
			}
			Debug.Log($"Octree of {_ctx.Colliders.Length} constructed (colliders: {elapsedMs}ms, tree: {sw.Elapsed.TotalMilliseconds}ms).");

			// create persistent kinematic and ball octrees (cleared + rebuilt each use)
			_ctx.KinematicOctree = new NativeOctree<int>(_ctx.PlayfieldBounds, 1024, 10, Allocator.Persistent);
			_ctx.BallOctree = new NativeOctree<int>(_ctx.PlayfieldBounds, 1024, 10, Allocator.Persistent);

			// create persistent physics cycle (holds contacts buffer)
			_ctx.PhysicsCycle = new PhysicsCycle(Allocator.Persistent);
			_ctx.SimulationNativeResourcesCreated = true;

			// get balls
			var balls = GetComponentsInChildren<BallComponent>();
			foreach (var ball in balls) {
				Register(ball);
			}

			// Create threading helper (all context fields are now populated)
			_threading = new PhysicsEngineThreading(this, _ctx, _player, _kinematicColliderComponents, _worldToPlayfield);

			// Mark as initialized for simulation thread
			_ctx.IsInitialized = true;
		}

		internal PhysicsState CreateState() => _ctx.CreateState();

		/// <summary>
		/// Enable external timing control (for simulation thread).
		/// When enabled, <see cref="Update"/> delegates to
		/// <see cref="PhysicsEngineThreading.DrainExternalThreadCallbacks"/>,
		/// <see cref="PhysicsEngineThreading.UpdateKinematicTransformsFromMainThread"/>,
		/// and <see cref="PhysicsEngineThreading.ApplyMovements"/>.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Main thread only (called during setup/teardown).
		/// </remarks>
		public void SetExternalTiming(bool enable)
		{
			_ctx.UseExternalTiming = enable;
		}

		/// <summary>
		/// Provide the triple-buffered <see cref="SimulationState"/> so that
		/// <see cref="PhysicsEngineThreading.SnapshotAnimations"/> can write
		/// animation data and
		/// <see cref="PhysicsEngineThreading.ApplyMovements"/> can read it
		/// lock-free.
		/// </summary>
		/// <remarks>
		/// <b>Thread:</b> Main thread only (called during setup/teardown).
		/// </remarks>
		public void SetSimulationState(SimulationState state)
		{
			_ctx.SimulationState = state;
		}

		/// <summary>
		/// Unity update loop. Dispatches to either the threaded or
		/// single-threaded code path via <see cref="PhysicsEngineThreading"/>.
		/// </summary>
		private void Update()
		{
			if (_threading == null) { // Start() hasn't completed yet
				return;
			}
			if (_ctx.UseExternalTiming) {
				// Simulation thread mode: physics runs on simulation thread,
				// but managed callbacks must run on Unity main thread.
				_threading.DrainExternalThreadCallbacks();

				// Collect kinematic transform changes on main thread and
				// stage them for the sim thread to apply.
				_threading.UpdateKinematicTransformsFromMainThread();

				_threading.ApplyMovements();
			} else {
				// Normal mode: Execute full physics update
				_threading.ExecutePhysicsUpdate(NowUsec);
			}
		}

		private void OnDestroy()
		{
			if (_ctx == null) {
				return;
			}

			StopSimulationThreadIfRunning();

			_ctx.IsInitialized = false;
			_ctx.UseExternalTiming = false;

			lock (_ctx.PhysicsLock) {
				_ctx.Dispose();
			}
		}

		private void OnDisable()
		{
			if (_ctx == null || !_ctx.UseExternalTiming) {
				return;
			}

			StopSimulationThreadIfRunning();
		}

		private void StopSimulationThreadIfRunning()
		{
			var simulationThreadComponent = GetComponent<SimulationThreadComponent>()
				?? GetComponentInParent<SimulationThreadComponent>()
				?? GetComponentInChildren<SimulationThreadComponent>();

			simulationThreadComponent?.StopSimulation();
		}

		#endregion

		public ICollider[] GetColliders(int itemId) => GetColliders(itemId, ref _ctx.ColliderLookups, ref _ctx.Colliders);
		public ICollider[] GetKinematicColliders(int itemId) => GetColliders(itemId, ref _ctx.KinematicColliderLookups, ref _ctx.KinematicColliders);

		private static ICollider[] GetColliders(int itemId, ref NativeParallelHashMap<int, NativeColliderIds> lookups, ref NativeColliders nativeColliders)
		{
			ref var colliderIds = ref lookups.GetValueByRef(itemId);
			var colliders = new ICollider[colliderIds.Length];
			for (var i = 0; i < colliderIds.Length; i++) {
				var colliderId = colliderIds[i];
				colliders[i] = nativeColliders[colliderId];
			}
			return colliders;
		}
	}
}
