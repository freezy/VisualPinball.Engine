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

using NativeTrees;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	[BurstCompile(CompileSynchronously = true)]
	internal struct PhysicsUpdateJob : IJob
	{
		[ReadOnly]
		public ulong InitialTimeUsec;

		public float DeltaTimeMs;

		[NativeDisableParallelForRestriction]
		public NativeParallelHashSet<int> OverlappingColliders;
		public NativeArray<PhysicsEnv> PhysicsEnv;
		public NativeOctree<int> Octree;
		public NativeColliders Colliders;
		public NativeColliders KinematicColliders;
		public NativeColliders KinematicCollidersAtIdentity;
		public NativeParallelHashMap<int, float4x4> KinematicTransforms;
		public NativeParallelHashMap<int, float4x4> UpdatedKinematicTransforms;
		public NativeParallelHashMap<int, float4x4> NonTransformableColliderMatrices;

		public NativeParallelHashMap<int, NativeColliderIds> KinematicColliderLookups;
		public InsideOfs InsideOfs;
		public NativeQueue<EventData>.ParallelWriter Events;
		public AABB PlayfieldBounds;

		public NativeParallelHashMap<int, BallState> Balls;
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
		public NativeParallelHashSet<int> DisabledCollisionItems;
		public bool SwapBallCollisionHandling;

		public void Execute()
		{
			var env = PhysicsEnv[0];
			var state = new PhysicsState(ref env, ref Octree, ref Colliders, ref KinematicColliders,
				ref KinematicCollidersAtIdentity, ref KinematicTransforms, ref UpdatedKinematicTransforms,
				ref NonTransformableColliderMatrices, ref KinematicColliderLookups, ref Events,
				ref InsideOfs, ref Balls, ref BumperStates, ref DropTargetStates, ref FlipperStates, ref GateStates,
				ref HitTargetStates, ref KickerStates, ref PlungerStates, ref SpinnerStates,
				ref SurfaceStates, ref TriggerStates, ref DisabledCollisionItems, ref SwapBallCollisionHandling);
			using var cycle = new PhysicsCycle(Allocator.Temp);

			// create octree of kinematic-to-ball collision. should be okay here, since kinetic colliders don't transform more than once per frame.
			PhysicsKinematics.TransformKinematicColliders(ref state);
			var kineticOctree = PhysicsKinematics.CreateOctree(ref state, in PlayfieldBounds);

			while (env.CurPhysicsFrameTime < InitialTimeUsec)  // loop here until current (real) time matches the physics (simulated) time
			{
				env.TimeMsec = (uint)((env.CurPhysicsFrameTime - env.StartTimeUsec) / 1000);
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
				cycle.Simulate(ref state, in PlayfieldBounds, ref OverlappingColliders, ref kineticOctree, physicsDiffTime);

				// ball trail, keep old pos of balls
				using (var enumerator = state.Balls.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						BallRingCounterPhysics.Update(ref enumerator.Current.Value);
					}
				}

				#region Animation

				// todo it should be enough to calculate animations only once per frame

				// bumper
				using (var enumerator = BumperStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var bumperState = ref enumerator.Current.Value;
						if (bumperState.RingItemId != 0) {
							BumperRingAnimation.Update(ref bumperState.RingAnimation, PhysicsConstants.PhysicsStepTime / 1000f);
						}
						if (bumperState.SkirtItemId != 0) {
							BumperSkirtAnimation.Update(ref bumperState.SkirtAnimation, PhysicsConstants.PhysicsStepTime / 1000f);
						}
					}
				}

				// drop target
				using (var enumerator = DropTargetStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var dropTargetState = ref enumerator.Current.Value;
						DropTargetAnimation.Update(enumerator.Current.Key, ref dropTargetState.Animation, in dropTargetState.Static, ref state);
					}
				}

				// hit target
				using (var enumerator = HitTargetStates.GetEnumerator()) {
					while (enumerator.MoveNext()) {
						ref var hitTargetState = ref enumerator.Current.Value;
						HitTargetAnimation.Update(ref hitTargetState.Animation, in hitTargetState.Static, env.TimeMsec);
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
						TriggerAnimation.Update(ref triggerState.Animation, ref triggerState.Movement, in triggerState.Static, PhysicsConstants.PhysicsStepTime / 1000f);
					}
				}

				#endregion

				env.CurPhysicsFrameTime = env.NextPhysicsFrameTime;
				env.NextPhysicsFrameTime += PhysicsConstants.PhysicsStepTime;
			}

			PhysicsEnv[0] = env;
			kineticOctree.Dispose();
		}
	}
}
