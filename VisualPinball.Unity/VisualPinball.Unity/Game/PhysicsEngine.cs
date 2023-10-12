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
using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Collections;
using VisualPinballUnity;
using Debug = UnityEngine.Debug;

namespace VisualPinball.Unity
{
	public class PhysicsEngine : MonoBehaviour
	{
		#region States

		[NonSerialized] private InsideOfs _insideOfs;
		[NonSerialized] private NativeArray<PhysicsEnv> _physicsEnv;
		[NonSerialized] private NativeOctree<int> _octree;
		[NonSerialized] private NativeQueue<EventData> _eventQueue;
		[NonSerialized] private BlobAssetReference<ColliderBlob> _colliders;

		[NonSerialized] private NativeParallelHashMap<int, BallData> _ballStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, BumperState> _bumperStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, FlipperState> _flipperStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, GateState> _gateStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, DropTargetState> _dropTargetStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, HitTargetState> _hitTargetStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, KickerState> _kickerStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, PlungerState> _plungerStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, SpinnerState> _spinnerStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, SurfaceState> _surfaceStates = new(0, Allocator.Persistent);
		[NonSerialized] private NativeParallelHashMap<int, TriggerState> _triggerStates = new(0, Allocator.Persistent);

		#endregion

		#region Transforms

		[NonSerialized] private readonly Dictionary<int, Transform> _transforms = new();
		[NonSerialized] private readonly Dictionary<int, SkinnedMeshRenderer[]> _skinnedMeshRenderers = new();

		#endregion

		[NonSerialized] private readonly Queue<InputAction> _inputActions = new();

		[NonSerialized] private Player _player;

		private static ulong NowUsec => (ulong)(Time.timeAsDouble * 1000000);

		#region API

		internal delegate void InputAction(ref PhysicsState state);

		internal void Schedule(InputAction action) => _inputActions.Enqueue(action);
		internal ref KickerState KickerState(int itemId) => ref _kickerStates.GetValueByRef(itemId);
		internal void SetBallInsideOf(int ballId, int itemId) => _insideOfs.SetInsideOf(itemId, ballId);

		internal void Register<T>(T item) where T : MonoBehaviour
		{
			var go = item.gameObject;
			var itemId = go.GetInstanceID();
			_transforms.TryAdd(itemId, go.transform);

			switch (item) {
				case BallComponent c:
					if (!_ballStates.ContainsKey(itemId)) {
						_ballStates[itemId] = c.CreateState();
					}
					break;
				case BumperComponent c: _bumperStates[itemId] = c.CreateState(); break;
				case FlipperComponent c: _flipperStates[itemId] = c.CreateState(); break;
				case GateComponent c: _gateStates[itemId] = c.CreateState(); break;
				case DropTargetComponent c: _dropTargetStates[itemId] = c.CreateState(); break;
				case HitTargetComponent c: _hitTargetStates[itemId] = c.CreateState(); break;
				case KickerComponent c: _kickerStates[itemId] = c.CreateState(); break;
				case PlungerComponent c:
					_plungerStates[itemId] = c.CreateState();
					_skinnedMeshRenderers[itemId] = c.GetComponentsInChildren<SkinnedMeshRenderer>();
					break;
				case SpinnerComponent c: _spinnerStates[itemId] = c.CreateState(); break;
				case SurfaceComponent c: _surfaceStates[itemId] = c.CreateState(); break;
				case TriggerComponent c: _triggerStates[itemId] = c.CreateState(); break;
			}
		}

		#endregion

		#region Event Functions

		private void Awake()
		{
			_player = GetComponent<Player>();
			_insideOfs = new InsideOfs(Allocator.Persistent);
		}

		private void Start()
		{
			// init state
			var env = new PhysicsEnv(NowUsec, GetComponent<Player>());

			// create static octree
			var sw = Stopwatch.StartNew();
			var colliderItems = GetComponentsInChildren<ICollidableComponent>();
			Debug.Log($"Found {colliderItems.Length} collidable items.");
			var colliders = new ColliderReference(Allocator.TempJob);
			foreach (var colliderItem in colliderItems) {
				colliderItem.GetColliders(_player, ref colliders, 0);
			}

			// allocate colliders
			_colliders = AllocateColliders(ref colliders);

			// create octree
			var elapsedMs = sw.Elapsed.TotalMilliseconds;
			var playfieldBounds = GetComponentInChildren<PlayfieldComponent>().Bounds;
			_octree = new NativeOctree<int>(playfieldBounds, 1024, 10, Allocator.Persistent);

			sw.Restart();
			var populateJob = new PopulatePhysicsJob {
				Colliders = _colliders,
				Octree = _octree,
			};
			populateJob.Run();
			_octree = populateJob.Octree;
			Debug.Log($"Octree of {_colliders.Value.Colliders.Length} constructed (colliders: {elapsedMs}ms, tree: {sw.Elapsed.TotalMilliseconds}ms).");

			// get balls
			var balls = GetComponentsInChildren<BallComponent>();
			foreach (var ball in balls) {
				Register(ball);
			}

			_eventQueue = new NativeQueue<EventData>(Allocator.Persistent);
			_physicsEnv = new NativeArray<PhysicsEnv>(1, Allocator.Persistent);
			_physicsEnv[0] = env;
		}

		private void Update()
		{
			// prepare job
			var events = _eventQueue.AsParallelWriter();
			var updatePhysics = new UpdatePhysicsJob {
				InitialTimeUsec = NowUsec,
				DeltaTimeMs = Time.deltaTime * 1000,
				PhysicsEnv = _physicsEnv,
				Octree = _octree,
				Colliders = _colliders,
				InsideOfs = _insideOfs,
				Events = events,
				Balls = _ballStates,
				BumperStates = _bumperStates,
				DropTargetStates = _dropTargetStates,
				FlipperStates = _flipperStates,
				GateStates = _gateStates,
				HitTargetStates = _hitTargetStates,
				KickerStates = _kickerStates,
				PlungerStates = _plungerStates,
				SpinnerStates = _spinnerStates,
				SurfaceStates = _surfaceStates,
				TriggerStates = _triggerStates,
			};

			var env = _physicsEnv[0];
			var state = new PhysicsState(ref env, ref _octree, ref _colliders, ref events, ref _insideOfs, ref _ballStates,
				ref _bumperStates, ref _dropTargetStates, ref _flipperStates, ref _gateStates,
				ref _hitTargetStates, ref _kickerStates, ref _plungerStates, ref _spinnerStates,
				ref _surfaceStates, ref _triggerStates);

			// process input
			while (_inputActions.Count > 0) {
				var action = _inputActions.Dequeue();
				action(ref state);
			}

			// run physics loop
			updatePhysics.Run();

			// dequeue events
			while (_eventQueue.TryDequeue(out var eventData)) {
				_player.OnEvent(in eventData);
			}

			// retrieve updated data
			_ballStates = updatePhysics.Balls;
			_physicsEnv = updatePhysics.PhysicsEnv;
			_flipperStates = updatePhysics.FlipperStates;

			#region Movements

			// balls
			using (var enumerator = state.Balls.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var ball = ref enumerator.Current.Value;
					BallMovementPhysics.Move(ball, _transforms[ball.Id]);
				}
			}

			// flippers
			using (var enumerator = _flipperStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var flipperState = ref enumerator.Current.Value;
					var flipperTransform = _transforms[enumerator.Current.Key];
					flipperTransform.localRotation = quaternion.Euler(0, flipperState.Movement.Angle, 0);
				}
			}

			// bumpers
			using (var enumerator = _bumperStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var bumperState = ref enumerator.Current.Value;
					if (bumperState.SkirtItemId != 0) {
						BumperTransform.UpdateSkirt(in bumperState.SkirtAnimation, _transforms[bumperState.SkirtItemId]);
					}
					if (bumperState.RingItemId != 0) {
						BumperTransform.UpdateRing(bumperState.RingItemId, in bumperState.RingAnimation, _transforms[bumperState.RingItemId]);
					}
				}
			}

			// drop targets
			using (var enumerator = _dropTargetStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var dropTargetState = ref enumerator.Current.Value;
					var dropTargetTransform = _transforms[dropTargetState.AnimatedItemId];
					var localPos = dropTargetTransform.localPosition;
					dropTargetTransform.localPosition = new Vector3(
						localPos.x,
						Physics.ScaleToWorld(dropTargetState.Animation.ZOffset),
						localPos.z
					);
				}
			}

			// hit targets
			using (var enumerator = _hitTargetStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var hitTargetState = ref enumerator.Current.Value;
					var hitTargetTransform = _transforms[hitTargetState.AnimatedItemId];
					var localRot = hitTargetTransform.localEulerAngles;
					hitTargetTransform.localEulerAngles = new Vector3(
						hitTargetState.Animation.XRotation,
						localRot.y,
						localRot.z
					);
				}
			}

			// gates
			using (var enumerator = _gateStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var gateState = ref enumerator.Current.Value;
					var gateTransform = _transforms[gateState.WireItemId];
					gateTransform.localRotation = quaternion.RotateX(-gateState.Movement.Angle);
				}
			}

			// plungers
			using (var enumerator = _plungerStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var plungerState = ref enumerator.Current.Value;
					foreach (var skinnedMeshRenderer in _skinnedMeshRenderers[enumerator.Current.Key]) {
						skinnedMeshRenderer.SetBlendShapeWeight(0, plungerState.Animation.Position);
					}
				}
			}

			// spinners
			using (var enumerator = _spinnerStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var spinnerState = ref enumerator.Current.Value;
					var spinnerTransform = _transforms[spinnerState.AnimationItemId];
					spinnerTransform.localRotation = quaternion.RotateX(-spinnerState.Movement.Angle);
				}
			}

			// triggers
			using (var enumerator = _triggerStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var triggerState = ref enumerator.Current.Value;
					if (triggerState.AnimatedItemId == 0) {
						continue;
					}
					var triggerTransform = _transforms[triggerState.AnimatedItemId];
					TriggerTransform.Update(triggerState.AnimatedItemId, in triggerState.Movement, triggerTransform);
				}
			}

			#endregion
		}
		
		private void OnDestroy()
		{
			_physicsEnv.Dispose();
			_eventQueue.Dispose();
			_ballStates.Dispose();
			_colliders.Dispose();
			_insideOfs.Dispose();
		}

		#endregion

		#region Helpers

		private static BlobAssetReference<ColliderBlob> AllocateColliders(ref ColliderReference managedColliders)
		{
			var allocateColliderJob = new ColliderAllocationJob(ref managedColliders);
			allocateColliderJob.Run();
			var colliders = allocateColliderJob.BlobAsset[0];
			allocateColliderJob.Dispose();
			return colliders;
		}

		#endregion
	}

	[BurstCompile(CompileSynchronously = true)]
	internal struct PopulatePhysicsJob : IJob
	{
		[ReadOnly]
		public BlobAssetReference<ColliderBlob> Colliders;
		public NativeOctree<int> Octree;
		
		public void Execute()
		{
			for (var i = 0; i < Colliders.Value.Colliders.Length; i++) {
				Octree.Insert(Colliders.GetId(i), Colliders.GetAabb(i));
			}
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	internal struct UpdatePhysicsJob : IJob
	{
		[ReadOnly] 
		public ulong InitialTimeUsec;

		public float DeltaTimeMs;

		public NativeArray<PhysicsEnv> PhysicsEnv;
		public NativeOctree<int> Octree;
		public BlobAssetReference<ColliderBlob> Colliders;
		public InsideOfs InsideOfs;
		public NativeQueue<EventData>.ParallelWriter Events;

		public NativeParallelHashMap<int, BallData> Balls;
		public NativeParallelHashMap<int, BumperState> BumperStates;
		public NativeParallelHashMap<int, DropTargetState> DropTargetStates;
		public NativeParallelHashMap<int, FlipperState> FlipperStates;
		public NativeParallelHashMap<int, GateState> GateStates;
		public NativeParallelHashMap<int, HitTargetState> HitTargetStates;
		public NativeParallelHashMap<int, KickerState> KickerStates;
		public NativeParallelHashMap<int, PlungerState> PlungerStates;
		public NativeParallelHashMap<int, SpinnerState> SpinnerStates;
		public NativeParallelHashMap<int, SurfaceState> SurfaceStates;
		public NativeParallelHashMap<int, TriggerState> TriggerStates;

		public void Execute()
		{
			var env = PhysicsEnv[0];
			var state = new PhysicsState(ref env, ref Octree, ref Colliders, ref Events, ref InsideOfs, ref Balls,
				ref BumperStates, ref DropTargetStates, ref FlipperStates, ref GateStates,
				ref HitTargetStates, ref KickerStates, ref PlungerStates, ref SpinnerStates,
				ref SurfaceStates, ref TriggerStates);
			var cycle = new PhysicsCycle(Allocator.Temp);

			while (env.CurPhysicsFrameTime < InitialTimeUsec)  // loop here until current (real) time matches the physics (simulated) time
			{
				var timeMsec = (uint)((env.CurPhysicsFrameTime - env.StartTimeUsec) / 1000);
				var physicsDiffTime = (float)((env.NextPhysicsFrameTime - env.CurPhysicsFrameTime) * (1.0 / PhysicsConstants.DefaultStepTime));

				// update velocities - always on integral physics frame boundary (spinner, gate, flipper, plunger, ball)
				#region Update Velocities

				// balls
				using (var enumerator = state.Balls.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						BallVelocityPhysics.UpdateVelocities(ref enumerator.Current.Value, env.Gravity);
					}
				}
				// flippers
				using (var enumerator = FlipperStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						FlipperVelocityPhysics.UpdateVelocities(ref enumerator.Current.Value);
					}
				}
				// gates
				using (var enumerator = GateStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var gateState = ref enumerator.Current.Value;
						GateVelocityPhysics.UpdateVelocities(ref gateState.Movement, in gateState.Static);
					}
				}
				// plungers
				using (var enumerator = PlungerStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var plungerState = ref enumerator.Current.Value;
						PlungerVelocityPhysics.UpdateVelocities(ref plungerState.Movement, ref plungerState.Velocity, in plungerState.Static);
					}
				}
				// spinners
				using (var enumerator = SpinnerStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var spinnerState = ref enumerator.Current.Value;
						SpinnerVelocityPhysics.UpdateVelocities(ref spinnerState.Movement, in spinnerState.Static);
					}
				}

				#endregion

				// primary physics loop
				cycle.Simulate(ref state, physicsDiffTime, timeMsec);

				// ball trail, keep old pos of balls
				using (var enumerator = state.Balls.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						BallRingCounterPhysics.Update(ref enumerator.Current.Value);
					}
				}

				#region Animation

				// bumper
				using (var enumerator = BumperStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var bumperState = ref enumerator.Current.Value;
						if (bumperState.RingItemId != 0) {
							BumperRingAnimation.Update(ref bumperState.RingAnimation, DeltaTimeMs);
						}
						if (bumperState.SkirtItemId != 0) {
							BumperSkirtAnimation.Update(ref bumperState.SkirtAnimation, DeltaTimeMs);
						}
					}
				}

				// drop target
				using (var enumerator = DropTargetStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var dropTargetState = ref enumerator.Current.Value;
						DropTargetAnimation.Update(enumerator.Current.Key, ref dropTargetState.Animation, in dropTargetState.Static, ref state, timeMsec);
					}
				}

				// hit target
				using (var enumerator = HitTargetStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var hitTargetState = ref enumerator.Current.Value;
						HitTargetAnimation.Update(ref hitTargetState.Animation, in hitTargetState.Static, timeMsec);
					}
				}

				// plunger
				using (var enumerator = PlungerStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var plungerState = ref enumerator.Current.Value;
						PlungerAnimation.Update(ref plungerState.Animation, in plungerState.Movement, in plungerState.Static);
					}
				}

				// trigger
				using (var enumerator = TriggerStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var triggerState = ref enumerator.Current.Value;
						TriggerAnimation.Update(ref triggerState.Animation, ref triggerState.Movement, in triggerState.Static, DeltaTimeMs);
					}
				}

				#endregion

				env.CurPhysicsFrameTime = env.NextPhysicsFrameTime;
				env.NextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;
			}

			PhysicsEnv[0] = env;
			cycle.Dispose();
		}
	}
}
