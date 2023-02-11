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
using System.Diagnostics;
using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.VisualPinball.Unity.Game;
using Debug = UnityEngine.Debug;

namespace VisualPinball.Unity
{
	public class PhysicsEngine : MonoBehaviour
	{
		[NonSerialized] private NativeArray<PhysicsState> _physicsState;
		[NonSerialized] private NativeOctree<PlaneCollider> _octree;
		[NonSerialized] private NativeList<BallData> _balls;

		private static ulong NowUsec => (ulong)(Time.timeAsDouble * 1000000);
		
		private void Start()
		{
			// init state
			_physicsState = new NativeArray<PhysicsState>(1, Allocator.Persistent);
			_physicsState[0] = new PhysicsState(NowUsec);

			// create static octree
			var sw = Stopwatch.StartNew();
			var colliderItems = GetComponentsInChildren<ICollidableComponent>();

			Debug.Log($"Found {colliderItems.Length} collider items.");
			var colliders = new NativeList<PlaneCollider>(Allocator.TempJob);
			foreach (var colliderItem in colliderItems) {
				colliderItem.GetColliders(ref colliders);
			}

			var elapsedMs = sw.Elapsed.TotalMilliseconds;
			var playfieldBounds = GetComponentInChildren<PlayfieldComponent>().Bounds;
			_octree = new NativeOctree<PlaneCollider>(playfieldBounds, 32, 10, Allocator.Persistent);
			
			sw.Restart();
			var populateJob = new PopulatePhysicsJob {
				Colliders = colliders,
				Octree = _octree, 
			};
			populateJob.Run();
			_octree = populateJob.Octree;
			Debug.Log($"Octree of {colliders.Length} constructed (colliders: {elapsedMs}ms, tree: {sw.Elapsed.TotalMilliseconds}ms).");
			colliders.Dispose();
			
			// get balls
			var balls = GetComponentsInChildren<PhysicsBall>();
			_balls = new NativeList<BallData>(balls.Length, Allocator.Persistent);
			foreach (var ball in balls) {
				_balls.Add(ball.Data);
			}
		}

		private void Update()
		{
			var updatePhysics = new UpdatePhysicsJob {
				InitialTimeUsec = NowUsec,
				PhysicsState = _physicsState,
				Octree = _octree,
				Balls = _balls,
			};
			
			updatePhysics.Run();
		}
		
		private void OnDestroy()
		{
			_physicsState.Dispose();
			_balls.Dispose();
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	internal struct PopulatePhysicsJob : IJob
	{
		[ReadOnly]
		public NativeList<PlaneCollider> Colliders;
		public NativeOctree<PlaneCollider> Octree;
		
		public void Execute()
		{
			foreach (var collider in Colliders) {
				Octree.Insert(collider, collider.Bounds);
			}
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	internal struct UpdatePhysicsJob : IJob
	{
		[ReadOnly] 
		public ulong InitialTimeUsec;

		public NativeArray<PhysicsState> PhysicsState;

		public NativeOctree<PlaneCollider> Octree;
		
		public NativeList<BallData> Balls;
		
		public void Execute()
		{
			var n = 0;
			var state = PhysicsState[0];
			var cycle = new PhysicsCycle(Allocator.Temp);
			
			while (state.CurPhysicsFrameTime < InitialTimeUsec)
			{
				var timeMsec = (uint)((state.CurPhysicsFrameTime - state.StartTimeUsec) / 1000);
				var physicsDiffTime = (float)((state.NextPhysicsFrameTime - state.CurPhysicsFrameTime) * (1.0 / PhysicsConstants.DefaultStepTime));

				cycle.Simulate(physicsDiffTime, ref state, ref Octree, ref Balls);
				
				state.CurPhysicsFrameTime = state.NextPhysicsFrameTime;
				state.NextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;

				n++;
			}

			PhysicsState[0] = state;
			cycle.Dispose();

			//Debug.Log($"UpdatePhysic {n}x");
		}
	}
}
