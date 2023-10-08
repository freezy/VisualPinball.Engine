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
		internal delegate void InputAction(ref PhysicsState state);

		[NonSerialized] private NativeArray<PhysicsEnv> _physicsEnv;
		[NonSerialized] private NativeOctree<int> _octree;
		[NonSerialized] private BlobAssetReference<ColliderBlob> _colliders;
		[NonSerialized] private NativeQueue<EventData> _eventQueue;
		[NonSerialized] private InsideOfs _insideOfs;
		[NonSerialized] private NativeParallelHashMap<int, BallData> _balls;
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

		[NonSerialized] private readonly Dictionary<int, PhysicsBall> _ballLookup = new();
		[NonSerialized] private readonly Dictionary<int, Transform> _transforms = new();

		[NonSerialized] private readonly Queue<InputAction> _inputActions = new();

		private static ulong NowUsec => (ulong)(Time.timeAsDouble * 1000000);

		internal void Register<T>(T item) where T : MonoBehaviour
		{
			var go = item.gameObject;
			var itemId = go.GetInstanceID();
			_transforms.TryAdd(itemId, go.transform);

			switch (item) {
				case BumperComponent c: _bumperStates[itemId] = c.CreateState(); break;
				case FlipperComponent c: _flipperStates[itemId] = c.CreateState(); break;
				case GateComponent c: _gateStates[itemId] = c.CreateState(); break;
				case DropTargetComponent c: _dropTargetStates[itemId] = c.CreateState(); break;
				case HitTargetComponent c: _hitTargetStates[itemId] = c.CreateState(); break;
				case KickerComponent c: _kickerStates[itemId] = c.CreateState(); break;
				case PlungerComponent c: _plungerStates[itemId] = c.CreateState(); break;
				case SpinnerComponent c: _spinnerStates[itemId] = c.CreateState(); break;
				case SurfaceComponent c: _surfaceStates[itemId] = c.CreateState(); break;
				case TriggerComponent c: _triggerStates[itemId] = c.CreateState(); break;
			}
		}

		internal void Schedule(InputAction action)
		{
			_inputActions.Enqueue(action);
		}

		private void Start()
		{
			var player = GetComponent<Player>();
			
			// init state
			var env = new PhysicsEnv(NowUsec, GetComponent<Player>());
			_insideOfs = new InsideOfs(Allocator.Persistent);

			// create static octree
			var sw = Stopwatch.StartNew();
			var colliderItems = GetComponentsInChildren<ICollidableComponent>();
			Debug.Log($"Found {colliderItems.Length} collidable items.");
			var colliders = new ColliderReference(Allocator.TempJob);
			foreach (var colliderItem in colliderItems) {
				colliderItem.GetColliders(player, ref colliders, 0);
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
			var balls = GetComponentsInChildren<PhysicsBall>();
			_balls = new NativeParallelHashMap<int, BallData>(balls.Length, Allocator.Persistent);
			foreach (var ball in balls) {
				_balls.Add(ball.Id, ball.Data);
				_ballLookup[ball.Id] = ball;
			}

			_eventQueue = new NativeQueue<EventData>(Allocator.Persistent);
			_physicsEnv = new NativeArray<PhysicsEnv>(1, Allocator.Persistent);
			_physicsEnv[0] = env;
		}

		private static BlobAssetReference<ColliderBlob> AllocateColliders(ref ColliderReference managedColliders)
		{
			var allocateColliderJob = new ColliderAllocationJob(ref managedColliders);
			allocateColliderJob.Run();
			var colliders = allocateColliderJob.BlobAsset[0];
			allocateColliderJob.Dispose();
			return colliders;
		}

		private void Update()
		{
			// prepare job
			var events = _eventQueue.AsParallelWriter();
			var updatePhysics = new UpdatePhysicsJob {
				InitialTimeUsec = NowUsec,
				DeltaTime = Time.deltaTime * 1000,
				PhysicsEnv = _physicsEnv,
				Octree = _octree,
				Colliders = _colliders,
				InsideOfs = _insideOfs,
				Events = events,
				Balls = _balls,
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
			var state = new PhysicsState(ref env, ref _octree, ref _colliders, ref events, ref _insideOfs, ref _balls,
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

			// retrieve updated data
			_balls = updatePhysics.Balls;
			_physicsEnv = updatePhysics.PhysicsEnv;
			_flipperStates = updatePhysics.FlipperStates;

			#region Movements

			// balls
			using (var enumerator = state.Balls.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var ball = ref enumerator.Current.Value;
					BallMovementPhysics.Move(ball, _ballLookup[ball.Id].transform);
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

			// gates
			using (var enumerator = _gateStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var gateState = ref enumerator.Current.Value;
					var gateTransform = _transforms[gateState.WireItemId];
					gateTransform.localRotation = quaternion.RotateX(-gateState.Movement.Angle);
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
			_balls.Dispose();
			_colliders.Dispose();
			_insideOfs.Dispose();
		}
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

		[ReadOnly]
		public float DeltaTime;

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

				#region Animation

				// bumper
				using (var enumerator = BumperStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var bumperState = ref enumerator.Current.Value;
						if (bumperState.RingItemId != 0) {
							BumperRingAnimation.Update(ref bumperState.RingAnimation, DeltaTime);
						}
						if (bumperState.SkirtItemId != 0) {
							BumperSkirtAnimation.Update(ref bumperState.SkirtAnimation, DeltaTime);
						}
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
