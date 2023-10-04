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
using VisualPinballUnity;
using Debug = UnityEngine.Debug;

namespace VisualPinball.Unity
{
	public class PhysicsEngine : MonoBehaviour
	{
		[NonSerialized] private NativeArray<PhysicsEnv> _physicsEnv;
		[NonSerialized] private NativeOctree<int> _octree;
		[NonSerialized] private BlobAssetReference<ColliderBlob> _colliders;
		[NonSerialized] private NativeQueue<EventData> _eventQueue;
		[NonSerialized] private InsideOfs _insideOfs;
		[NonSerialized] private NativeParallelHashMap<int, BallData> _balls;
		[NonSerialized] private NativeParallelHashMap<int, FlipperState> _flipperStates;
		[NonSerialized] private NativeParallelHashMap<int, BumperState> _bumperStates;

		[NonSerialized] private readonly Dictionary<int, PhysicsBall> _ballLookup = new();
		[NonSerialized] private readonly Dictionary<int, Transform> _transforms = new();

		[NonSerialized] internal readonly Queue<InputAction> InputActions = new();
		internal delegate void InputAction(ref PhysicsState state);

		private static ulong NowUsec => (ulong)(Time.timeAsDouble * 1000000);

		public void Register<T>(T item) where T : MonoBehaviour
		{
			var go = item.gameObject;
			_transforms.Add(go.GetInstanceID(), go.transform);
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
			var managedColliders = new List<ICollider>();
			foreach (var colliderItem in colliderItems) {
				// todo bring GC allocations down
				colliderItem.GetColliders(player, managedColliders, 0);
			}

			#region Item Data

			// bumpers
			var bumpers = GetComponentsInChildren<BumperComponent>();
			_bumperStates = new NativeParallelHashMap<int, BumperState>(bumpers.Length, Allocator.Persistent);
			foreach (var bumper in bumpers) {
				var bumperState = bumper.CreateState();
				_bumperStates[bumperState.ItemId] = bumperState;
			}

			// flippers
			var flippers = GetComponentsInChildren<FlipperComponent>();
			_flipperStates = new NativeParallelHashMap<int, FlipperState>(flippers.Length, Allocator.Persistent);
			foreach (var flipper in flippers) {
				var flipperState = flipper.CreateState();
				_flipperStates[flipperState.ItemId] = flipperState;
			}

			#endregion

			// allocate colliders
			_colliders = AllocateColliders(managedColliders);

			// create octree
			var elapsedMs = sw.Elapsed.TotalMilliseconds;
			var playfieldBounds = GetComponentInChildren<PlayfieldComponent>().Bounds;
			_octree = new NativeOctree<int>(playfieldBounds, 32, 10, Allocator.Persistent);

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

		private static BlobAssetReference<ColliderBlob> AllocateColliders(IEnumerable<ICollider> managedColliders)
		{
			var allocateColliderJob = new ColliderAllocationJob(managedColliders);
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
				FlipperStates = _flipperStates,
				BumperStates = _bumperStates,
			};

			var env = _physicsEnv[0];
			var state = new PhysicsState(ref env, ref _octree, ref _colliders, ref events, ref _insideOfs, ref _balls, ref _flipperStates, ref _bumperStates);

			// process input
			while (InputActions.Count > 0) {
				var action = InputActions.Dequeue();
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
					var flipperTransform = _transforms[enumerator.Current.Key];
					flipperTransform.localRotation = quaternion.Euler(0, _flipperStates[enumerator.Current.Key].Movement.Angle, 0);
				}
			}

			// bumpers
			using (var enumerator = _bumperStates.GetEnumerator()) {
				while (enumerator.MoveNext()) {
					ref var bumperState = ref enumerator.Current.Value;
					if (bumperState.SkirtItemId != 0) {
						BumperTransformation.UpdateSkirt(in bumperState.SkirtAnimation, _transforms[bumperState.SkirtItemId]);
					}
					if (bumperState.RingItemId != 0) {
						BumperTransformation.UpdateRing(bumperState.RingItemId, in bumperState.RingAnimation, _transforms[bumperState.RingItemId]);
					}
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
		public NativeParallelHashMap<int, FlipperState> FlipperStates;
		public NativeParallelHashMap<int, BumperState> BumperStates;

		public void Execute()
		{
			var env = PhysicsEnv[0];
			var state = new PhysicsState(ref env, ref Octree, ref Colliders, ref Events, ref InsideOfs, ref Balls, ref FlipperStates, ref BumperStates);
			var cycle = new PhysicsCycle(Allocator.Temp);

			while (env.CurPhysicsFrameTime < InitialTimeUsec)  // loop here until current (real) time matches the physics (simulated) time
			{
				var timeMsec = (uint)((env.CurPhysicsFrameTime - env.StartTimeUsec) / 1000);
				var physicsDiffTime = (float)((env.NextPhysicsFrameTime - env.CurPhysicsFrameTime) * (1.0 / PhysicsConstants.DefaultStepTime));

				#region Update Velocities

				// update velocities - always on integral physics frame boundary (spinner, gate, flipper, plunger, ball)
				using (var enumerator = state.Balls.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						BallVelocityPhysics.UpdateVelocities(ref enumerator.Current.Value, env.Gravity);
					}
				}
				using (var enumerator = FlipperStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						FlipperVelocityPhysics.UpdateVelocities(ref enumerator.Current.Value);
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
