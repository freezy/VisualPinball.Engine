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

using System;
using System.Collections.Generic;
using NativeTrees;
using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Shared mutable state for the physics engine.
	///
	/// <para>This object is the single source of truth for all physics data
	/// that <see cref="PhysicsEngine"/> (lifecycle / public API) and
	/// <see cref="PhysicsEngineThreading"/> (tick execution / movement
	/// application) both need. Extracting it into a dedicated class
	/// makes the data contract explicit and prevents hidden coupling
	/// through partial-class field sharing.</para>
	///
	/// <para><b>Ownership:</b> Created eagerly as a field initializer on
	/// <see cref="PhysicsEngine"/> so it is available before any
	/// <c>Awake</c> calls (Unity does not guarantee <c>Awake</c> order).
	/// Populated in <c>Awake</c>/<c>Start</c>. Passed by reference to
	/// <see cref="PhysicsEngineThreading"/> at the end of <c>Start</c>.
	/// Disposed in <c>OnDestroy</c>.</para>
	/// </summary>
	internal class PhysicsEngineContext : IDisposable
	{
		#region Physics World

		public AABB PlayfieldBounds;
		public InsideOfs InsideOfs;
		public NativeOctree<int> Octree;

		/// <summary>
		/// Persistent octree for kinematic-to-ball collision detection.
		/// </summary>
		/// <remarks>
		/// Created once in <see cref="PhysicsEngine.Start"/> with
		/// <c>Allocator.Persistent</c>. Cleared and rebuilt only when
		/// <see cref="KinematicOctreeDirty"/> is set, rather than every
		/// physics tick. This reduces overhead from ~1 kHz to ~60 Hz.
		/// </remarks>
		public NativeOctree<int> KinematicOctree;

		/// <summary>
		/// Whether the kinematic octree needs to be rebuilt before the
		/// next physics tick. Set to <c>true</c> when kinematic transforms
		/// change (either via pending staging in threaded mode or via
		/// direct detection in single-threaded mode). Initialized to
		/// <c>true</c> so the first tick builds the octree.
		/// </summary>
		public bool KinematicOctreeDirty = true;

		/// <summary>
		/// Persistent octree for ball-to-ball collision detection.
		/// </summary>
		/// <remarks>
		/// Created once in <see cref="PhysicsEngine.Start"/> with
		/// <c>Allocator.Persistent</c>. Cleared and rebuilt every
		/// physics cycle (ball positions change every tick), but avoids
		/// per-tick allocation/deallocation overhead.
		/// </remarks>
		public NativeOctree<int> BallOctree;

		/// <summary>
		/// Persistent physics cycle struct, holding the contacts buffer.
		/// </summary>
		/// <remarks>
		/// Created once in <see cref="PhysicsEngine.Start"/> with
		/// <c>Allocator.Persistent</c>. Avoids per-tick allocation of
		/// the internal <c>NativeList&lt;ContactBufferElement&gt;</c>.
		/// </remarks>
		public PhysicsCycle PhysicsCycle;
		public bool SimulationNativeResourcesCreated;

		public NativeColliders Colliders;
		public NativeColliders KinematicColliders;
		public NativeColliders KinematicCollidersAtIdentity;
		public NativeParallelHashMap<int, NativeColliderIds> KinematicColliderLookups;
		public NativeParallelHashMap<int, NativeColliderIds> ColliderLookups; // only used for editor debug
		public NativeParallelHashSet<int> OverlappingColliders = new(0, Allocator.Persistent);
		public PhysicsEnv PhysicsEnv;
		public bool SwapBallCollisionHandling;

		#endregion

		#region Per-Item State Maps

		public readonly LazyInit<NativeQueue<EventData>> EventQueue = new(() => new NativeQueue<EventData>(Allocator.Persistent));
		public readonly LazyInit<NativeParallelHashMap<int, BallState>> BallStates = new(() => new NativeParallelHashMap<int, BallState>(0, Allocator.Persistent));
		public readonly LazyInit<NativeParallelHashMap<int, BumperState>> BumperStates = new(() => new NativeParallelHashMap<int, BumperState>(0, Allocator.Persistent));
		public readonly LazyInit<NativeParallelHashMap<int, FlipperState>> FlipperStates = new(() => new NativeParallelHashMap<int, FlipperState>(0, Allocator.Persistent));
		public readonly LazyInit<NativeParallelHashMap<int, GateState>> GateStates = new(() => new NativeParallelHashMap<int, GateState>(0, Allocator.Persistent));
		public readonly LazyInit<NativeParallelHashMap<int, DropTargetState>> DropTargetStates = new(() => new NativeParallelHashMap<int, DropTargetState>(0, Allocator.Persistent));
		public readonly LazyInit<NativeParallelHashMap<int, HitTargetState>> HitTargetStates = new(() => new NativeParallelHashMap<int, HitTargetState>(0, Allocator.Persistent));
		public readonly LazyInit<NativeParallelHashMap<int, KickerState>> KickerStates = new(() => new NativeParallelHashMap<int, KickerState>(0, Allocator.Persistent));
		public readonly LazyInit<NativeParallelHashMap<int, PlungerState>> PlungerStates = new(() => new NativeParallelHashMap<int, PlungerState>(0, Allocator.Persistent));
		public readonly LazyInit<NativeParallelHashMap<int, SpinnerState>> SpinnerStates = new(() => new NativeParallelHashMap<int, SpinnerState>(0, Allocator.Persistent));
		public readonly LazyInit<NativeParallelHashMap<int, SurfaceState>> SurfaceStates = new(() => new NativeParallelHashMap<int, SurfaceState>(0, Allocator.Persistent));
		public readonly LazyInit<NativeParallelHashMap<int, TriggerState>> TriggerStates = new(() => new NativeParallelHashMap<int, TriggerState>(0, Allocator.Persistent));
		public readonly LazyInit<NativeParallelHashSet<int>> DisabledCollisionItems = new(() => new NativeParallelHashSet<int>(0, Allocator.Persistent));

		public NativeParallelHashMap<int, FixedList512Bytes<float>> ElasticityOverVelocityLUTs;
		public NativeParallelHashMap<int, FixedList512Bytes<float>> FrictionOverVelocityLUTs;

		#endregion

		#region Transforms & Animation Components

		/// <summary>
		/// Main-thread-only lookup: ballId -> BallComponent for applying
		/// visual movement from snapshots.
		/// </summary>
		public readonly Dictionary<int, BallComponent> BallComponents = new();

		/// <summary>
		/// Last transforms of kinematic items, so we can detect changes.
		/// </summary>
		/// <remarks>
		/// Written by: sim thread (via <c>ApplyPendingKinematicTransforms</c>)
		/// or main thread (single-threaded mode).
		/// </remarks>
		public readonly LazyInit<NativeParallelHashMap<int, float4x4>> KinematicTransforms = new(() => new NativeParallelHashMap<int, float4x4>(0, Allocator.Persistent));

		/// <summary>
		/// The transforms of kinematic items that have changed since the last frame.
		/// </summary>
		/// <remarks>
		/// Written by: sim thread (inside <c>PhysicsLock</c>) or main thread
		/// (single-threaded mode). Read by: physics loop.
		/// </remarks>
		public readonly LazyInit<NativeParallelHashMap<int, float4x4>> UpdatedKinematicTransforms = new(() => new NativeParallelHashMap<int, float4x4>(0, Allocator.Persistent));

		/// <summary>
		/// The current matrix to which the ball will be transformed to, if
		/// it collides with a non-transformable collider. This changes as
		/// the non-transformable collider transforms (it's called
		/// non-transformable as in not transformable by the physics engine,
		/// but it can be transformed by the game).
		/// </summary>
		public readonly LazyInit<NativeParallelHashMap<int, float4x4>> NonTransformableColliderTransforms = new(() => new NativeParallelHashMap<int, float4x4>(0, Allocator.Persistent));

		/// <summary>
		/// Main-thread-only lookup: itemId -> animation emitter for float
		/// values (flipper angle, gate angle, plunger position, etc.).
		/// </summary>
		public readonly Dictionary<int, IAnimationValueEmitter<float>> FloatAnimatedComponents = new();

		/// <summary>
		/// Main-thread-only lookup: itemId -> animation emitter for float2
		/// values (bumper skirt rotation).
		/// </summary>
		public readonly Dictionary<int, IAnimationValueEmitter<float2>> Float2AnimatedComponents = new();

		#endregion

		#region Cross-Thread Communication

		/// <summary>
		/// Input actions enqueued by component APIs (any thread) and drained
		/// by the sim thread inside <c>PhysicsLock</c>.
		/// Protected by <see cref="InputActionsLock"/>.
		/// </summary>
		public readonly Queue<PhysicsEngine.InputAction> InputActions = new();
		public readonly object InputActionsLock = new();

		/// <summary>
		/// Scheduled managed callbacks stored in a min-heap by due time.
		/// Protected by locking on <see cref="ScheduledActionsLock"/>.
		/// </summary>
		public readonly List<ScheduledAction> ScheduledActions = new();
		public readonly object ScheduledActionsLock = new();

		/// <summary>
		/// Reference to the triple-buffered simulation state owned by the
		/// <see cref="SimulationThread"/>. Set via
		/// <see cref="PhysicsEngine.SetSimulationState"/> after the thread
		/// is created. Null when running in single-threaded mode.
		/// </summary>
		public SimulationState SimulationState;

		/// <summary>
		/// Staging area for kinematic transform updates computed on the
		/// main thread. Protected by <see cref="PendingKinematicLock"/>.
		/// </summary>
		public readonly LazyInit<NativeParallelHashMap<int, float4x4>> PendingKinematicTransforms = new(() => new NativeParallelHashMap<int, float4x4>(0, Allocator.Persistent));

		/// <summary>
		/// Lock protecting <see cref="PendingKinematicTransforms"/>.
		/// Lock ordering: sim thread may hold <c>PhysicsLock</c> then
		/// acquire <c>PendingKinematicLock</c>.
		/// </summary>
		public readonly object PendingKinematicLock = new();

		/// <summary>
		/// Main-thread-only cache of last-reported kinematic transforms,
		/// used to detect changes without reading
		/// <see cref="KinematicTransforms"/> (which the sim thread writes).
		/// </summary>
		public readonly Dictionary<int, float4x4> MainThreadKinematicCache = new();

		/// <summary>
		/// Whether to use external timing (simulation thread) or Unity's
		/// Time. When true, <see cref="PhysicsEngine.Update"/> delegates
		/// physics to the simulation thread.
		/// </summary>
		public bool UseExternalTiming;

		/// <summary>
		/// Coarse lock held by the sim thread for the duration of each
		/// tick. Main thread acquires non-blockingly via
		/// <c>Monitor.TryEnter</c> to drain callbacks.
		/// </summary>
		public readonly object PhysicsLock = new();

		/// <summary>
		/// Whether physics engine is fully initialized and ready for the
		/// simulation thread.
		/// </summary>
		public volatile bool IsInitialized;

		/// <summary>
		/// Accumulated physics busy time in microseconds. Updated
		/// atomically via <see cref="System.Threading.Interlocked.Add"/>
		/// from any thread.
		/// </summary>
		public long PhysicsBusyTotalUsec;
		public long PublishedPhysicsFrameTimeUsec;
		public long LastKinematicScanUsec;
		public long LastEventDrainUsec;

		#endregion

		#region Methods

		/// <summary>
		/// Create a <see cref="PhysicsState"/> snapshot that references the
		/// live native containers. Used by both the simulation thread and
		/// the single-threaded main-thread path.
		/// </summary>
		internal PhysicsState CreateState()
		{
			var events = EventQueue.Ref.AsParallelWriter();
			return new PhysicsState(ref PhysicsEnv, ref Octree, ref Colliders, ref KinematicColliders,
				ref KinematicCollidersAtIdentity, ref KinematicTransforms.Ref, ref UpdatedKinematicTransforms.Ref,
				ref NonTransformableColliderTransforms.Ref, ref KinematicColliderLookups, ref events,
				ref InsideOfs, ref BallStates.Ref, ref BumperStates.Ref, ref DropTargetStates.Ref, ref FlipperStates.Ref, ref GateStates.Ref,
				ref HitTargetStates.Ref, ref KickerStates.Ref, ref PlungerStates.Ref, ref SpinnerStates.Ref,
				ref SurfaceStates.Ref, ref TriggerStates.Ref, ref DisabledCollisionItems.Ref, ref SwapBallCollisionHandling,
				ref ElasticityOverVelocityLUTs, ref FrictionOverVelocityLUTs);
		}

		/// <summary>
		/// Dispose all native collections. Called from
		/// <see cref="PhysicsEngine.OnDestroy"/>.
		/// </summary>
		public void Dispose()
		{
			OverlappingColliders.Dispose();
			EventQueue.Ref.Dispose();
			BallStates.Ref.Dispose();
			ElasticityOverVelocityLUTs.Dispose();
			FrictionOverVelocityLUTs.Dispose();
			Colliders.Dispose();
			KinematicColliders.Dispose();
			KinematicCollidersAtIdentity.Dispose();
			InsideOfs.Dispose();
			if (SimulationNativeResourcesCreated) {
				Octree.Dispose();
				KinematicOctree.Dispose();
				BallOctree.Dispose();
				PhysicsCycle.Dispose();
				SimulationNativeResourcesCreated = false;
			}
			BumperStates.Ref.Dispose();
			DropTargetStates.Ref.Dispose();
			FlipperStates.Ref.Dispose();
			GateStates.Ref.Dispose();
			HitTargetStates.Ref.Dispose();

			using (var enumerator = KickerStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					enumerator.Current.Value.Dispose();
				}
			}
			KickerStates.Ref.Dispose();

			PlungerStates.Ref.Dispose();
			SpinnerStates.Ref.Dispose();
			SurfaceStates.Ref.Dispose();

			using (var enumerator = TriggerStates.Ref.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					enumerator.Current.Value.Dispose();
				}
			}
			TriggerStates.Ref.Dispose();

			DisabledCollisionItems.Ref.Dispose();
			KinematicTransforms.Ref.Dispose();
			UpdatedKinematicTransforms.Ref.Dispose();
			PendingKinematicTransforms.Ref.Dispose();
			NonTransformableColliderTransforms.Ref.Dispose();

			using (var enumerator = KinematicColliderLookups.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					enumerator.Current.Value.Dispose();
				}
			}
			KinematicColliderLookups.Dispose();

			if (ColliderLookups.IsCreated) {
				using (var enumerator = ColliderLookups.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						enumerator.Current.Value.Dispose();
					}
				}
				ColliderLookups.Dispose();
			}
		}

		#endregion

		#region Nested Types

		/// <summary>
		/// A managed callback scheduled for future execution at a specific
		/// physics time.
		/// </summary>
		internal class ScheduledAction
		{
			public readonly ulong ScheduleAt;
			public readonly Action Action;

			public ScheduledAction(ulong scheduleAt, Action action)
			{
				ScheduleAt = scheduleAt;
				Action = action;
			}
		}

		#endregion
	}
}
