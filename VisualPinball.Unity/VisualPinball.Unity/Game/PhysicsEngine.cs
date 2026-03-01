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

		#region States

		[NonSerialized] private AABB _playfieldBounds;
		[NonSerialized] private InsideOfs _insideOfs;
		[NonSerialized] private NativeOctree<int> _octree;
		[NonSerialized] private NativeColliders _colliders;
		[NonSerialized] private NativeColliders _kinematicColliders;
		[NonSerialized] private NativeColliders _kinematicCollidersAtIdentity;
		[NonSerialized] private NativeParallelHashMap<int, NativeColliderIds> _kinematicColliderLookups;
		[NonSerialized] private NativeParallelHashMap<int, NativeColliderIds> _colliderLookups; // only used for editor debug
		[NonSerialized] private NativeParallelHashSet<int> _overlappingColliders = new(0, Allocator.Persistent);
		[NonSerialized] private PhysicsEnv _physicsEnv;
		[NonSerialized] private readonly LazyInit<NativeQueue<EventData>> _eventQueue = new(() => new NativeQueue<EventData>(Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, BallState>> _ballStates = new(() => new NativeParallelHashMap<int, BallState>(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, BumperState>> _bumperStates = new(() => new NativeParallelHashMap<int, BumperState>(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, FlipperState>> _flipperStates = new(() => new NativeParallelHashMap<int, FlipperState>(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, GateState>> _gateStates = new(() => new NativeParallelHashMap<int, GateState>(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, DropTargetState>>_dropTargetStates = new(() => new NativeParallelHashMap<int, DropTargetState>(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, HitTargetState>> _hitTargetStates = new(() => new NativeParallelHashMap<int, HitTargetState>(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, KickerState>> _kickerStates = new(() => new NativeParallelHashMap<int, KickerState>(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, PlungerState>> _plungerStates = new(() => new NativeParallelHashMap<int, PlungerState>(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, SpinnerState>> _spinnerStates = new(() => new NativeParallelHashMap<int, SpinnerState>(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, SurfaceState>> _surfaceStates = new(() => new NativeParallelHashMap<int, SurfaceState>(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, TriggerState>> _triggerStates = new(() => new NativeParallelHashMap<int, TriggerState>(0, Allocator.Persistent));
		[NonSerialized] private readonly LazyInit<NativeParallelHashSet<int>> _disabledCollisionItems = new(() => new NativeParallelHashSet<int>(0, Allocator.Persistent));
		[NonSerialized] private bool _swapBallCollisionHandling;

		[NonSerialized] public NativeParallelHashMap<int, FixedList512Bytes<float>> ElasticityOverVelocityLUTs;
		[NonSerialized] public NativeParallelHashMap<int, FixedList512Bytes<float>> FrictionOverVelocityLUTs;

		#endregion

		#region Transforms

		[NonSerialized] private readonly Dictionary<int, BallComponent> _ballComponents = new();

		/// <summary>
		/// Last transforms of kinematic items, so we can detect changes.
		/// </summary>
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, float4x4>> _kinematicTransforms = new(() => new NativeParallelHashMap<int, float4x4>(0, Allocator.Persistent));

		/// <summary>
		/// The transforms of the kinematic items that have changes since the last frame.
		/// </summary>
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, float4x4>> _updatedKinematicTransforms = new(() => new NativeParallelHashMap<int, float4x4>(0, Allocator.Persistent));

		/// <summary>
		/// The current matrix to which the ball will be transformed to, if it collides with a non-transformable collider.
		/// This changes as the non-transformable collider collider transforms (it's called non-transformable as in
		/// not transformable by the physics engine, but it can be transformed by the game).
		///
		/// todo save inverse matrix, too
		/// </summary>
		/// <remarks>
		/// This has nothing to do with kinematic transformations, it's purely to add full support for transformations
		/// for items where the original physics engine doesn't.
		/// </remarks>
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, float4x4>> _nonTransformableColliderTransforms = new(() => new NativeParallelHashMap<int, float4x4>(0, Allocator.Persistent));

		[NonSerialized] private readonly Dictionary<int, IAnimationValueEmitter<bool>> _boolAnimatedComponents = new();
		[NonSerialized] private readonly Dictionary<int, IAnimationValueEmitter<float>> _floatAnimatedComponents = new();
		[NonSerialized] private readonly Dictionary<int, IAnimationValueEmitter<float2>> _float2AnimatedComponents = new();

		#endregion

		[NonSerialized] private readonly Queue<InputAction> _inputActions = new();
		private readonly object _inputActionsLock = new object();
		[NonSerialized] private readonly List<ScheduledAction> _scheduledActions = new();

		[NonSerialized] private Player _player;
		[NonSerialized] private PhysicsMovements _physicsMovements;
		[NonSerialized] private ICollidableComponent[] _colliderComponents;
		[NonSerialized] private ICollidableComponent[] _kinematicColliderComponents;
		[NonSerialized] private float4x4 _worldToPlayfield;

		/// <summary>
		/// Reference to the triple-buffered simulation state owned by the
		/// SimulationThread. Set via <see cref="SetSimulationState"/> after
		/// the thread is created. Null when running in single-threaded mode.
		/// </summary>
		[NonSerialized] private SimulationState _simulationState;

		#region Kinematic Pending Buffer (Fix 2)

		/// <summary>
		/// Staging area for kinematic transform updates computed on the main
		/// thread. Protected by <see cref="_pendingKinematicLock"/>.
		/// </summary>
		[NonSerialized] private readonly LazyInit<NativeParallelHashMap<int, float4x4>> _pendingKinematicTransforms = new(() => new NativeParallelHashMap<int, float4x4>(0, Allocator.Persistent));

		/// <summary>
		/// Lock protecting <see cref="_pendingKinematicTransforms"/>.
		/// Lock ordering: sim thread may hold _physicsLock then acquire
		/// _pendingKinematicLock. Main thread only holds _pendingKinematicLock.
		/// </summary>
		private readonly object _pendingKinematicLock = new object();

		/// <summary>
		/// Main-thread-only cache of last-reported kinematic transforms,
		/// used to detect changes without reading _kinematicTransforms (which
		/// the sim thread writes).
		/// </summary>
		[NonSerialized] private readonly Dictionary<int, float4x4> _mainThreadKinematicCache = new();

		#endregion

		private static ulong NowUsec => (ulong)(Time.timeAsDouble * 1000000);

		/// <summary>
		/// Current physics time in microseconds (for external tick support)
		/// </summary>
		private ulong _externalTimeUsec = 0;

		/// <summary>
		/// Whether to use external timing (simulation thread) or Unity's Time
		/// </summary>
		private bool _useExternalTiming = false;

		/// <summary>
		/// Lock for synchronizing physics state access between threads
		/// </summary>
		private readonly object _physicsLock = new object();

		/// <summary>
		/// Whether physics engine is fully initialized and ready for simulation thread
		/// </summary>
		private volatile bool _isInitialized = false;

		/// <summary>
		/// Check if the physics engine has completed initialization.
		/// Used by the simulation thread to wait for physics to be ready.
		/// </summary>
		public bool IsInitialized => _isInitialized;

		private float _lastFrameTimeMs;
		private long _physicsBusyTotalUsec;

		#region API

		public void ScheduleAction(int timeoutMs, Action action) => ScheduleAction((uint)timeoutMs, action);
		public void ScheduleAction(uint timeoutMs, Action action)
		{
			lock (_scheduledActions) {
				_scheduledActions.Add(new ScheduledAction(_physicsEnv.CurPhysicsFrameTime + (ulong)timeoutMs * 1000, action));
			}
		}

		internal delegate void InputAction(ref PhysicsState state);

		internal ref NativeParallelHashMap<int, BallState> Balls => ref _ballStates.Ref;
		internal ref InsideOfs InsideOfs => ref _insideOfs;
		internal NativeQueue<EventData>.ParallelWriter EventQueue => _eventQueue.Ref.AsParallelWriter();

		internal void Schedule(InputAction action)
		{
			lock (_inputActionsLock) {
				_inputActions.Enqueue(action);
			}
		}
		internal bool BallExists(int ballId) => _ballStates.Ref.ContainsKey(ballId);
		internal ref BallState BallState(int ballId) => ref _ballStates.Ref.GetValueByRef(ballId);
		internal ref BumperState BumperState(int itemId) => ref _bumperStates.Ref.GetValueByRef(itemId);
		internal ref FlipperState FlipperState(int itemId) => ref _flipperStates.Ref.GetValueByRef(itemId);
		internal ref GateState GateState(int itemId) => ref _gateStates.Ref.GetValueByRef(itemId);
		internal ref DropTargetState DropTargetState(int itemId) => ref _dropTargetStates.Ref.GetValueByRef(itemId);
		internal ref HitTargetState HitTargetState(int itemId) => ref _hitTargetStates.Ref.GetValueByRef(itemId);
		internal ref KickerState KickerState(int itemId) => ref _kickerStates.Ref.GetValueByRef(itemId);
		internal ref PlungerState PlungerState(int itemId) => ref _plungerStates.Ref.GetValueByRef(itemId);
		internal ref SpinnerState SpinnerState(int itemId) => ref _spinnerStates.Ref.GetValueByRef(itemId);
		internal ref SurfaceState SurfaceState(int itemId) => ref _surfaceStates.Ref.GetValueByRef(itemId);
		internal ref TriggerState TriggerState(int itemId) => ref _triggerStates.Ref.GetValueByRef(itemId);
		internal void SetBallInsideOf(int ballId, int itemId) => _insideOfs.SetInsideOf(itemId, ballId);
		internal bool HasBallsInsideOf(int itemId) => _insideOfs.GetInsideCount(itemId) > 0;
		internal List<int> GetBallsInsideOf(int itemId) => _insideOfs.GetIdsOfBallsInsideItem(itemId);

		internal uint TimeMsec => _physicsEnv.TimeMsec;
		internal Random Random => _physicsEnv.Random;
		internal void Register<T>(T item) where T : MonoBehaviour
		{
			var go = item.gameObject;
			var itemId = go.GetInstanceID();

			// states
			switch (item) {
				case BallComponent c:
					if (!_ballStates.Ref.ContainsKey(itemId)) {
						_ballStates.Ref[itemId] = c.CreateState();
					}
					_ballComponents.TryAdd(itemId, c);
					break;
				case BumperComponent c: _bumperStates.Ref[itemId] = c.CreateState(); break;
				case FlipperComponent c:
					_flipperStates.Ref[itemId] = c.CreateState();
					break;
				case GateComponent c: _gateStates.Ref[itemId] = c.CreateState(); break;
				case DropTargetComponent c: _dropTargetStates.Ref[itemId] = c.CreateState(); break;
				case HitTargetComponent c: _hitTargetStates.Ref[itemId] = c.CreateState(); break;
				case KickerComponent c: _kickerStates.Ref[itemId] = c.CreateState(); break;
				case PlungerComponent c:
					_plungerStates.Ref[itemId] = c.CreateState();
					break;
				case SpinnerComponent c: _spinnerStates.Ref[itemId] = c.CreateState(); break;
				case SurfaceComponent c: _surfaceStates.Ref[itemId] = c.CreateState(); break;
				case TriggerComponent c: _triggerStates.Ref[itemId] = c.CreateState(); break;
			}

			// animations
			if (item is IAnimationValueEmitter<bool> boolAnimatedComponent) {
				_boolAnimatedComponents.TryAdd(itemId, boolAnimatedComponent);
			}
			if (item is IAnimationValueEmitter<float> floatAnimatedComponent) {
				_floatAnimatedComponents.TryAdd(itemId, floatAnimatedComponent);
			}
			if (item is IAnimationValueEmitter<float2> float2AnimatedComponent) {
				_float2AnimatedComponents.TryAdd(itemId, float2AnimatedComponent);
			}
		}

		internal BallComponent UnregisterBall(int ballId)
		{
			var b = _ballComponents[ballId];
			_ballComponents.Remove(ballId);
			_ballStates.Ref.Remove(ballId);
			_insideOfs.SetOutsideOfAll(ballId);
			return b;
		}

		internal bool IsColliderEnabled(int itemId) => !_disabledCollisionItems.Ref.Contains(itemId);

		internal void EnableCollider(int itemId)
		{
			if (_disabledCollisionItems.Ref.Contains(itemId)) {
				_disabledCollisionItems.Ref.Remove(itemId);
			}
		}
		internal void DisableCollider(int itemId)
		{
			if (!_disabledCollisionItems.Ref.Contains(itemId)) {
				_disabledCollisionItems.Ref.Add(itemId);
			}
		}

		public BallComponent GetBall(int itemId) => _ballComponents[itemId];

		#endregion

		#region Event Functions

		private void Awake()
		{
			_player = GetComponentInParent<Player>();
			_physicsMovements = new PhysicsMovements();
			_insideOfs = new InsideOfs(Allocator.Persistent);
			_physicsEnv = new PhysicsEnv(NowUsec, GetComponentInChildren<PlayfieldComponent>(), GravityStrength);
			_colliderComponents = GetComponentsInChildren<ICollidableComponent>();
			_kinematicColliderComponents = _colliderComponents.Where(c => c.IsKinematic).ToArray();
			ElasticityOverVelocityLUTs = new NativeParallelHashMap<int, FixedList512Bytes<float>>(0, Allocator.Persistent);
			FrictionOverVelocityLUTs = new NativeParallelHashMap<int, FixedList512Bytes<float>>(0, Allocator.Persistent);
		}

		private void Start()
		{
			var sw = Stopwatch.StartNew();
			var playfield = GetComponentInChildren<PlayfieldComponent>();

			// register frame pacing stats
			var stats = FindFirstObjectByType<FramePacingGraph>();
			if (stats) {
				long lastBusyTotalUsec = Interlocked.Read(ref _physicsBusyTotalUsec);
				InputLatencyTracker.Reset();
				stats.RegisterCustomMetric("Physics", Color.magenta, () => {
					var totalBusyUsec = Interlocked.Read(ref _physicsBusyTotalUsec);
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
			var colliders = new ColliderReference(ref _nonTransformableColliderTransforms.Ref, Allocator.Temp);
			var kinematicColliders = new ColliderReference(ref _nonTransformableColliderTransforms.Ref, Allocator.Temp, true);
			foreach (var colliderItem in _colliderComponents) {
				if (!colliderItem.IsCollidable) {
					_disabledCollisionItems.Ref.Add(colliderItem.ItemId);
				}

				var translateWithinPlayfieldMatrix = colliderItem.GetLocalToPlayfieldMatrixInVpx(playfield.transform.worldToLocalMatrix);
				// todo check if we cannot only add those that are actually non-transformable
				_nonTransformableColliderTransforms.Ref[colliderItem.ItemId] = translateWithinPlayfieldMatrix;

				if (colliderItem.IsKinematic) {
					colliderItem.GetColliders(_player, this, ref kinematicColliders, translateWithinPlayfieldMatrix, 0);
				} else {
					colliderItem.GetColliders(_player, this, ref colliders, translateWithinPlayfieldMatrix, 0);
				}
			}

			// allocate colliders
			_colliders = new NativeColliders(ref colliders, Allocator.Persistent);
			_kinematicColliders = new NativeColliders(ref kinematicColliders, Allocator.Persistent);

			// get kinetic collider matrices
			_worldToPlayfield = playfield.transform.worldToLocalMatrix;
			foreach (var coll in _kinematicColliderComponents) {
				var matrix = coll.GetLocalToPlayfieldMatrixInVpx(_worldToPlayfield);
				_kinematicTransforms.Ref[coll.ItemId] = matrix;
				_mainThreadKinematicCache[coll.ItemId] = matrix;
			}
#if UNITY_EDITOR
			_colliderLookups = colliders.CreateLookup(Allocator.Persistent);
#endif
			_kinematicColliderLookups = kinematicColliders.CreateLookup(Allocator.Persistent);

			// create identity kinematic colliders
			kinematicColliders.TransformToIdentity(ref _kinematicTransforms.Ref);
			_kinematicCollidersAtIdentity = new NativeColliders(ref kinematicColliders, Allocator.Persistent);

			// create octree
			var elapsedMs = sw.Elapsed.TotalMilliseconds;
			_playfieldBounds = playfield.Bounds;
			_octree = new NativeOctree<int>(_playfieldBounds, 1024, 10, Allocator.Persistent);

			sw.Restart();
			unsafe {
				fixed (NativeColliders* c = &_colliders)
				fixed (NativeOctree<int>* o = &_octree) {
					PhysicsPopulate.PopulateUnsafe((IntPtr)c, (IntPtr)o);
				}
			}
			Debug.Log($"Octree of {_colliders.Length} constructed (colliders: {elapsedMs}ms, tree: {sw.Elapsed.TotalMilliseconds}ms).");

			// get balls
			var balls = GetComponentsInChildren<BallComponent>();
			foreach (var ball in balls) {
				Register(ball);
			}

			// Mark as initialized for simulation thread
			_isInitialized = true;
		}

		internal PhysicsState CreateState()
		{
			var events = _eventQueue.Ref.AsParallelWriter();
			return new PhysicsState(ref _physicsEnv, ref _octree, ref _colliders, ref _kinematicColliders,
				ref _kinematicCollidersAtIdentity, ref _kinematicTransforms.Ref, ref _updatedKinematicTransforms.Ref,
				ref _nonTransformableColliderTransforms.Ref, ref _kinematicColliderLookups, ref events,
				ref _insideOfs, ref _ballStates.Ref, ref _bumperStates.Ref, ref _dropTargetStates.Ref, ref _flipperStates.Ref, ref _gateStates.Ref,
				ref _hitTargetStates.Ref, ref _kickerStates.Ref, ref _plungerStates.Ref, ref _spinnerStates.Ref,
				ref _surfaceStates.Ref, ref _triggerStates.Ref, ref _disabledCollisionItems.Ref, ref _swapBallCollisionHandling,
				ref ElasticityOverVelocityLUTs, ref FrictionOverVelocityLUTs);

		}

		/// <summary>
		/// Enable external timing control (for simulation thread).
		/// When enabled, Update() does nothing and ExecuteTick() must be called instead.
		/// </summary>
		/// <param name="enable">Whether to enable external timing</param>
		public void SetExternalTiming(bool enable)
		{
			_useExternalTiming = enable;
			if (enable) {
				_externalTimeUsec = (ulong)(Time.timeAsDouble * 1000000);
			}
		}

		/// <summary>
		/// Provide the triple-buffered SimulationState so that
		/// <see cref="SnapshotAnimations"/> can write animation data and
		/// <see cref="ApplyMovementsFromSnapshot"/> can read it lock-free.
		/// </summary>
		public void SetSimulationState(SimulationState state)
		{
			_simulationState = state;
		}

		/// <summary>
		/// Execute a single physics tick with external timing (for simulation thread).
		/// This runs the physics simulation but does NOT apply movements to GameObjects.
		/// Call ApplyMovements() from the main thread to update transforms.
		/// </summary>
		/// <param name="timeUsec">Current time in microseconds</param>
		public void ExecuteTick(ulong timeUsec)
		{
			// Wait until physics engine is fully initialized
			if (!_isInitialized) {
				return;
			}

			lock (_physicsLock) {
				_externalTimeUsec = timeUsec;
				ExecutePhysicsSimulation(timeUsec);
			}
		}

		/// <summary>
		/// Apply physics state to GameObjects (must be called from main thread).
		/// When a <see cref="SimulationState"/> has been set via
		/// <see cref="SetSimulationState"/>, this reads the latest published
		/// snapshot — completely lock-free. Otherwise falls back to the legacy
		/// lock-based path.
		/// </summary>
		public void ApplyMovements()
		{
			if (!_useExternalTiming || !_isInitialized) return;

			if (_simulationState != null) {
				// Lock-free path: read from triple-buffered snapshot
				ref readonly var snapshot = ref _simulationState.AcquireReadBuffer();
				ApplyMovementsFromSnapshot(in snapshot);
			} else {
				// Legacy path (no SimulationState set — shouldn't happen in
				// normal operation but kept as safety net).
				if (!Monitor.TryEnter(_physicsLock)) {
					return; // sim thread is mid-tick; skip this frame
				}
				try {
					var state = CreateState();
					ApplyAllMovements(ref state);
				} finally {
					Monitor.Exit(_physicsLock);
				}
			}
		}

		private void Update()
		{
			if (_useExternalTiming) {
				// Simulation thread mode: physics runs on simulation thread,
				// but managed callbacks must run on Unity main thread.
				DrainExternalThreadCallbacks();

				// Collect kinematic transform changes on main thread and
				// stage them for the sim thread to apply (Fix 2).
				UpdateKinematicTransformsFromMainThread();

				ApplyMovements();
			} else {
				// Normal mode: Execute full physics update
				ExecutePhysicsUpdate(NowUsec);
			}
		}

		/// <summary>
		/// Drain physics-originated managed callbacks on the Unity main thread.
		/// Non-blocking: if the simulation thread currently holds the physics
		/// lock, callbacks are deferred to the next frame.
		/// </summary>
		private void DrainExternalThreadCallbacks()
		{
			if (!_useExternalTiming || !_isInitialized) {
				return;
			}

			if (!Monitor.TryEnter(_physicsLock)) {
				return; // sim thread is mid-tick; drain next frame
			}
			try {
				while (_eventQueue.Ref.TryDequeue(out var eventData)) {
					_player.OnEvent(in eventData);
				}

				lock (_scheduledActions) {
					for (var i = _scheduledActions.Count - 1; i >= 0; i--) {
						if (_physicsEnv.CurPhysicsFrameTime > _scheduledActions[i].ScheduleAt) {
							_scheduledActions[i].Action();
							_scheduledActions.RemoveAt(i);
						}
					}
				}
			} finally {
				Monitor.Exit(_physicsLock);
			}
		}

		/// <summary>
		/// Core physics simulation (can be called from simulation thread).
		/// Does NOT apply movements to GameObjects - only updates physics state.
		/// </summary>
		private void ExecutePhysicsSimulation(ulong currentTimeUsec)
		{
			var sw = Stopwatch.StartNew();

			// Apply kinematic transform updates staged by main thread (Fix 2).
			ApplyPendingKinematicTransforms();

			var state = CreateState();

			// process input
			ProcessInputActions(ref state);

			// run physics loop (Burst-compiled, thread-safe)
			PhysicsUpdate.Execute(
				ref state,
				ref _physicsEnv,
				ref _overlappingColliders,
				_playfieldBounds,
				currentTimeUsec
			);

			RecordPhysicsBusyTime(sw.ElapsedTicks);
		}

		/// <summary>
		/// Full physics update (main thread only - includes movement application)
		/// </summary>
		private void ExecutePhysicsUpdate(ulong currentTimeUsec)
		{
			var sw = Stopwatch.StartNew();

			// check for updated kinematic transforms
			_updatedKinematicTransforms.Ref.Clear();
			foreach (var coll in _kinematicColliderComponents) {
				var lastTransformationMatrix = _kinematicTransforms.Ref[coll.ItemId];
				var currTransformationMatrix = coll.GetLocalToPlayfieldMatrixInVpx(_worldToPlayfield);
				if (lastTransformationMatrix.Equals(currTransformationMatrix)) {
					continue;
				}
				_updatedKinematicTransforms.Ref.Add(coll.ItemId, currTransformationMatrix);
				_kinematicTransforms.Ref[coll.ItemId] = currTransformationMatrix;
				coll.OnTransformationChanged(currTransformationMatrix);
			}

			var state = CreateState();

			// process input
			ProcessInputActions(ref state);

			// run physics loop
			PhysicsUpdate.Execute(
				ref state,
				ref _physicsEnv,
				ref _overlappingColliders,
				_playfieldBounds,
				currentTimeUsec
			);

			// dequeue events
			while (_eventQueue.Ref.TryDequeue(out var eventData)) {
				_player.OnEvent(in eventData);
			}

			// process scheduled events from managed land
			lock (_scheduledActions) {
				for (var i = _scheduledActions.Count - 1; i >= 0; i--) {
					if (_physicsEnv.CurPhysicsFrameTime > _scheduledActions[i].ScheduleAt) {
						_scheduledActions[i].Action();
						_scheduledActions.RemoveAt(i);
					}
				}
			}

			// Apply movements to GameObjects
			ApplyAllMovements(ref state);

			RecordPhysicsBusyTime(sw.ElapsedTicks);
		}

		private void RecordPhysicsBusyTime(long elapsedTicks)
		{
			var elapsedUsec = (elapsedTicks * 1_000_000L) / Stopwatch.Frequency;
			if (elapsedUsec < 0) {
				elapsedUsec = 0;
			}

			Interlocked.Add(ref _physicsBusyTotalUsec, elapsedUsec);
			_lastFrameTimeMs = elapsedUsec / 1000f;
		}

		/// <summary>
		/// Apply all physics movements to GameObjects (main thread only)
		/// </summary>
		private void ApplyAllMovements(ref PhysicsState state)
		{
			_physicsMovements.ApplyBallMovement(ref state, _ballComponents);
			_physicsMovements.ApplyFlipperMovement(ref _flipperStates.Ref, _floatAnimatedComponents);
			_physicsMovements.ApplyBumperMovement(ref _bumperStates.Ref, _floatAnimatedComponents, _float2AnimatedComponents);
			_physicsMovements.ApplyDropTargetMovement(ref _dropTargetStates.Ref, _floatAnimatedComponents);
			_physicsMovements.ApplyHitTargetMovement(ref _hitTargetStates.Ref, _floatAnimatedComponents);
			_physicsMovements.ApplyGateMovement(ref _gateStates.Ref, _floatAnimatedComponents);
			_physicsMovements.ApplyPlungerMovement(ref _plungerStates.Ref, _floatAnimatedComponents);
			_physicsMovements.ApplySpinnerMovement(ref _spinnerStates.Ref, _floatAnimatedComponents);
			_physicsMovements.ApplyTriggerMovement(ref _triggerStates.Ref, _floatAnimatedComponents);
		}

		#region Snapshot-Based Movement (Fix 1)

		/// <summary>
		/// Copy current animation values from physics state maps into the
		/// given snapshot buffer. Called on the sim thread AFTER
		/// <see cref="ExecuteTick"/> returns (sequential within the thread,
		/// so reading physics state maps is safe without an extra lock).
		/// MUST BE ALLOCATION-FREE.
		/// </summary>
		internal void SnapshotAnimations(ref SimulationState.Snapshot snapshot)
		{
			// --- Balls ---
			var ballCount = 0;
			using (var enumerator = _ballStates.Ref.GetEnumerator()) {
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
			using (var enumerator = _flipperStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && floatCount < SimulationState.MaxFloatAnimations) {
					ref var s = ref enumerator.Current.Value;
					snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
						ItemId = enumerator.Current.Key, Value = s.Movement.Angle
					};
				}
			}

			// Bumper rings (float) — ring animation
			using (var enumerator = _bumperStates.Ref.GetEnumerator()) {
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
			using (var enumerator = _dropTargetStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && floatCount < SimulationState.MaxFloatAnimations) {
					ref var s = ref enumerator.Current.Value;
					if (s.AnimatedItemId == 0) continue;
					snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
						ItemId = enumerator.Current.Key, Value = s.Animation.ZOffset
					};
				}
			}

			// Hit targets
			using (var enumerator = _hitTargetStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && floatCount < SimulationState.MaxFloatAnimations) {
					ref var s = ref enumerator.Current.Value;
					if (s.AnimatedItemId == 0) continue;
					snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
						ItemId = enumerator.Current.Key, Value = s.Animation.XRotation
					};
				}
			}

			// Gates
			using (var enumerator = _gateStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && floatCount < SimulationState.MaxFloatAnimations) {
					ref var s = ref enumerator.Current.Value;
					snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
						ItemId = enumerator.Current.Key, Value = s.Movement.Angle
					};
				}
			}

			// Plungers
			using (var enumerator = _plungerStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && floatCount < SimulationState.MaxFloatAnimations) {
					ref var s = ref enumerator.Current.Value;
					snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
						ItemId = enumerator.Current.Key, Value = s.Animation.Position
					};
				}
			}

			// Spinners
			using (var enumerator = _spinnerStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext() && floatCount < SimulationState.MaxFloatAnimations) {
					ref var s = ref enumerator.Current.Value;
					snapshot.FloatAnimations[floatCount++] = new SimulationState.FloatAnimation {
						ItemId = enumerator.Current.Key, Value = s.Movement.Angle
					};
				}
			}

			// Triggers
			using (var enumerator = _triggerStates.Ref.GetEnumerator()) {
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
			using (var enumerator = _bumperStates.Ref.GetEnumerator()) {
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

		/// <summary>
		/// Apply visual updates from a snapshot — called on the main thread,
		/// completely lock-free. Replaces the legacy
		/// <see cref="ApplyAllMovements"/> path when in external timing mode.
		/// </summary>
		private void ApplyMovementsFromSnapshot(in SimulationState.Snapshot snapshot)
		{
			// Balls
			for (var i = 0; i < snapshot.BallCount; i++) {
				var bs = snapshot.BallSnapshots[i];
				if (bs.IsFrozen != 0) continue;
				if (_ballComponents.TryGetValue(bs.Id, out var ballComponent)) {
					// Reconstruct a lightweight BallState with the fields Move() needs.
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
				if (_floatAnimatedComponents.TryGetValue(anim.ItemId, out var emitter)) {
					emitter.UpdateAnimationValue(anim.Value);
				}
			}

			// Float2 animations
			for (var i = 0; i < snapshot.Float2AnimationCount; i++) {
				var anim = snapshot.Float2Animations[i];
				if (_float2AnimatedComponents.TryGetValue(anim.ItemId, out var emitter)) {
					emitter.UpdateAnimationValue(anim.Value);
				}
			}
		}

		#endregion

		#region Kinematic Transform Staging (Fix 2)

		/// <summary>
		/// Collect kinematic transform changes on the Unity main thread and
		/// stage them in <see cref="_pendingKinematicTransforms"/> for the sim
		/// thread to apply. Only called when <see cref="_useExternalTiming"/>
		/// is true. Uses <see cref="_mainThreadKinematicCache"/> for change
		/// detection (never reads <see cref="_kinematicTransforms"/> which the
		/// sim thread writes).
		/// </summary>
		internal void UpdateKinematicTransformsFromMainThread()
		{
			if (!_useExternalTiming || !_isInitialized || _kinematicColliderComponents == null) return;

			foreach (var coll in _kinematicColliderComponents) {
				var currMatrix = coll.GetLocalToPlayfieldMatrixInVpx(_worldToPlayfield);

				// Check against main-thread cache
				if (_mainThreadKinematicCache.TryGetValue(coll.ItemId, out var lastMatrix) && lastMatrix.Equals(currMatrix)) {
					continue;
				}

				// Transform changed — update cache
				_mainThreadKinematicCache[coll.ItemId] = currMatrix;

				// Notify the component (e.g. KickerColliderComponent updates its
				// center). NOTE: this writes physics state from the main thread,
				// which is a pre-existing thread-safety issue inherited from the
				// original code. A future improvement would schedule these as
				// input actions.
				coll.OnTransformationChanged(currMatrix);

				// Stage for the sim thread
				lock (_pendingKinematicLock) {
					_pendingKinematicTransforms.Ref[coll.ItemId] = currMatrix;
				}
			}
		}

		/// <summary>
		/// Apply kinematic transforms staged by the main thread into the
		/// physics state maps. Called on the sim thread inside
		/// <see cref="ExecutePhysicsSimulation"/> (inside _physicsLock), so
		/// writing to <see cref="_updatedKinematicTransforms"/> and
		/// <see cref="_kinematicTransforms"/> is safe.
		/// Lock ordering: _physicsLock (held) → _pendingKinematicLock (inner).
		/// </summary>
		internal void ApplyPendingKinematicTransforms()
		{
			if (!_pendingKinematicTransforms.Ref.IsCreated) return;

			_updatedKinematicTransforms.Ref.Clear();

			lock (_pendingKinematicLock) {
				if (_pendingKinematicTransforms.Ref.Count() == 0) return;

				using var enumerator = _pendingKinematicTransforms.Ref.GetEnumerator();
				while (enumerator.MoveNext()) {
					var itemId = enumerator.Current.Key;
					var matrix = enumerator.Current.Value;
					_updatedKinematicTransforms.Ref[itemId] = matrix;
					_kinematicTransforms.Ref[itemId] = matrix;
				}
				_pendingKinematicTransforms.Ref.Clear();
			}
		}

		#endregion

		private void ProcessInputActions(ref PhysicsState state)
		{
			lock (_inputActionsLock) {
				while (_inputActions.Count > 0) {
					var action = _inputActions.Dequeue();
					action(ref state);
				}
			}
		}
		
		private void OnDestroy()
		{
			_overlappingColliders.Dispose();
			_eventQueue.Ref.Dispose();
			_ballStates.Ref.Dispose();
			ElasticityOverVelocityLUTs.Dispose();
			FrictionOverVelocityLUTs.Dispose();
			_colliders.Dispose();
			_kinematicColliders.Dispose();
			_insideOfs.Dispose();
			_octree.Dispose();
			_bumperStates.Ref.Dispose();
			_dropTargetStates.Ref.Dispose();
			_flipperStates.Ref.Dispose();
			_gateStates.Ref.Dispose();
			_hitTargetStates.Ref.Dispose();
			using (var enumerator = _kickerStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					enumerator.Current.Value.Dispose();
				}
			}
			_kickerStates.Ref.Dispose();
			_plungerStates.Ref.Dispose();
			_spinnerStates.Ref.Dispose();
			_surfaceStates.Ref.Dispose();
			using (var enumerator = _triggerStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					enumerator.Current.Value.Dispose();
				}
			}
			_triggerStates.Ref.Dispose();
			_disabledCollisionItems.Ref.Dispose();
			_kinematicTransforms.Ref.Dispose();
			_updatedKinematicTransforms.Ref.Dispose();
			_pendingKinematicTransforms.Ref.Dispose();
			using (var enumerator = _kinematicColliderLookups.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					enumerator.Current.Value.Dispose();
				}
			}
			_kinematicColliderLookups.Dispose();
			using (var enumerator = _colliderLookups.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					enumerator.Current.Value.Dispose();
				}
			}
			_colliderLookups.Dispose();
		}

		#endregion

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

		public ICollider[] GetColliders(int itemId) => GetColliders(itemId, ref _colliderLookups, ref _colliders);
		public ICollider[] GetKinematicColliders(int itemId) => GetColliders(itemId, ref _kinematicColliderLookups, ref _kinematicColliders);

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
