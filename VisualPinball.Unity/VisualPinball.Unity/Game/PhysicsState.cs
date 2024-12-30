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
using NativeTrees;
using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	internal struct PhysicsState
	{
		internal PhysicsEnv Env;
		internal NativeOctree<int> Octree;
		internal NativeColliders Colliders;
		internal NativeColliders KinematicColliders;
		internal NativeColliders KinematicCollidersAtIdentity;
		internal NativeParallelHashMap<int, float4x4> UpdatedKinematicTransforms; // updated transformations of the items, in vpx space.
		internal NativeParallelHashMap<int, float4x4> KinematicTransforms;        // transformations of the items, in vpx space.
		internal NativeParallelHashMap<int, float4x4> NonTransformableColliderMatrices;
		internal NativeParallelHashMap<int, NativeColliderIds> KinematicColliderLookups;

		internal NativeQueue<EventData>.ParallelWriter EventQueue;
		internal InsideOfs InsideOfs;
		internal NativeParallelHashMap<int, BallState> Balls;
		internal NativeParallelHashMap<int, BumperState> BumperStates;
		internal NativeParallelHashMap<int, DropTargetState> DropTargetStates;
		internal NativeParallelHashMap<int, FlipperState> FlipperStates;
		internal NativeParallelHashMap<int, GateState> GateStates;
		internal NativeParallelHashMap<int, HitTargetState> HitTargetStates;
		internal NativeParallelHashMap<int, KickerState> KickerStates;
		internal NativeParallelHashMap<int, PlungerState> PlungerStates;
		internal NativeParallelHashMap<int, SpinnerState> SpinnerStates;
		internal NativeParallelHashMap<int, SurfaceState> SurfaceStates;
		internal NativeParallelHashMap<int, TriggerState> TriggerStates;
		internal NativeParallelHashSet<int> DisabledCollisionItems;
		internal bool SwapBallCollisionHandling;

		public PhysicsState(ref PhysicsEnv env, ref NativeOctree<int> octree, ref NativeColliders colliders,
			ref NativeColliders kinematicColliders, ref NativeColliders kinematicCollidersAtIdentity,
			ref NativeParallelHashMap<int, float4x4> kinematicTransforms,
			ref NativeParallelHashMap<int, float4x4> updatedKinematicTransforms,
			ref NativeParallelHashMap<int, float4x4> nonTransformableColliderMatrices,
			ref NativeParallelHashMap<int, NativeColliderIds> kinematicColliderLookups, ref NativeQueue<EventData>.ParallelWriter eventQueue,
			ref InsideOfs insideOfs, ref NativeParallelHashMap<int, BallState> balls,
			ref NativeParallelHashMap<int, BumperState> bumperStates, ref NativeParallelHashMap<int, DropTargetState> dropTargetStates,
			ref NativeParallelHashMap<int, FlipperState> flipperStates, ref NativeParallelHashMap<int, GateState> gateStates,
			ref NativeParallelHashMap<int, HitTargetState> hitTargetStates, ref NativeParallelHashMap<int, KickerState> kickerStates,
			ref NativeParallelHashMap<int, PlungerState> plungerStates, ref NativeParallelHashMap<int, SpinnerState> spinnerStates,
			ref NativeParallelHashMap<int, SurfaceState> surfaceStates, ref NativeParallelHashMap<int, TriggerState> triggerStates,
			ref NativeParallelHashSet<int> disabledCollisionItems, ref bool swapBallCollisionHandling)
		{
			Env = env;
			Octree = octree;
			Colliders = colliders;
			KinematicColliders = kinematicColliders;
			KinematicCollidersAtIdentity = kinematicCollidersAtIdentity;
			KinematicTransforms = kinematicTransforms;
			UpdatedKinematicTransforms = updatedKinematicTransforms;
			NonTransformableColliderMatrices = nonTransformableColliderMatrices;
			KinematicColliderLookups = kinematicColliderLookups;
			EventQueue = eventQueue;
			InsideOfs = insideOfs;
			Balls = balls;
			BumperStates = bumperStates;
			DropTargetStates = dropTargetStates;
			FlipperStates = flipperStates;
			GateStates = gateStates;
			HitTargetStates = hitTargetStates;
			KickerStates = kickerStates;
			PlungerStates = plungerStates;
			SpinnerStates = spinnerStates;
			SurfaceStates = surfaceStates;
			TriggerStates = triggerStates;
			DisabledCollisionItems = disabledCollisionItems;
			SwapBallCollisionHandling = swapBallCollisionHandling;
		}

		internal ref ColliderHeader GetColliderHeader(ref NativeColliders colliders, int colliderId) => ref colliders.GetHeader(colliderId);
		internal ColliderType GetColliderType(ref NativeColliders colliders, int colliderId) => colliders.GetHeader(colliderId).Type;

		internal bool IsColliderActive(ref NativeColliders colliders, int colliderId) => !DisabledCollisionItems.Contains(colliders.GetItemId(colliderId));

		#region States

		internal ref FlipperState GetFlipperState(int colliderId, ref NativeColliders colliders) => ref FlipperStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref PlungerState GetPlungerState(int colliderId, ref NativeColliders colliders) => ref PlungerStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref SpinnerState GetSpinnerState(int colliderId, ref NativeColliders colliders) => ref SpinnerStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref TriggerState GetTriggerState(int colliderId, ref NativeColliders colliders) => ref TriggerStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref KickerState GetKickerState(int colliderId, ref NativeColliders colliders) => ref KickerStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal bool HasDropTargetState(int colliderId, ref NativeColliders colliders) => DropTargetStates.ContainsKey(colliders.GetItemId(colliderId));

		internal bool HasHitTargetState(int colliderId, ref NativeColliders colliders) => HitTargetStates.ContainsKey(colliders.GetItemId(colliderId));

		internal ref DropTargetState GetDropTargetState(int colliderId, ref NativeColliders colliders) => ref DropTargetStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref HitTargetState GetHitTargetState(int colliderId, ref NativeColliders colliders) => ref HitTargetStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref BumperState GetBumperState(int colliderId, ref NativeColliders colliders) => ref BumperStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref GateState GetGateState(int colliderId, ref NativeColliders colliders) => ref GateStates.GetValueByRef(colliders.GetItemId(colliderId));

		internal ref SurfaceState GetSurfaceState(int colliderId, ref NativeColliders colliders) => ref SurfaceStates.GetValueByRef(colliders.GetItemId(colliderId));

		#endregion

		#region Transform

		internal ref float4x4 GetNonTransformableColliderMatrix(int colliderId, ref NativeColliders colliders)
		{
			var itemId = colliders.GetItemId(colliderId);
			if (colliders.KinematicColliders) {
				return ref KinematicTransforms.GetValueByRef(itemId);
			}
			return ref NonTransformableColliderMatrices.GetValueByRef(itemId);
		}

		/// <summary>
		/// Transforms a collider with a given transformation matrix. The matrix can be anything,
		/// so colliders here must be 100% transformable (i.e. ICollider.IsFullyTransformable = true).
		///
		/// </summary>
		/// <param name="colliderId">The ID of the collider</param>
		/// <param name="matrix">The transformation matrix</param>
		internal void Transform(int colliderId, float4x4 matrix)
		{
			switch (GetColliderType(ref KinematicColliders, colliderId)) {
				case ColliderType.Point:
					var pointCollider = KinematicColliders.Point(colliderId);
					pointCollider.Transform(KinematicCollidersAtIdentity.Point(colliderId), matrix);
					break;

				case ColliderType.Line3D:
					var line3DCollider = KinematicColliders.Line3D(colliderId);
					line3DCollider.Transform(KinematicCollidersAtIdentity.Line3D(colliderId), matrix);
					break;

				case ColliderType.Triangle:
					var triangleCollider = KinematicColliders.Triangle(colliderId);
					triangleCollider.Transform(KinematicCollidersAtIdentity.Triangle(colliderId), matrix);
					break;
			}
		}

		#endregion

		#region Hit Test

		internal float HitTest(ref NativeColliders colliders, int colliderId, ref BallState ball, ref CollisionEventData newCollEvent, ref NativeList<ContactBufferElement> contacts)
		{
			if (IsInactiveDropTarget(ref colliders, colliderId)) {
				return -1f;
			}
			switch (GetColliderType(ref colliders, colliderId)) {
				case ColliderType.Bumper:
					return colliders.Circle(colliderId).HitTestBasicRadius(ref newCollEvent, ref InsideOfs, in ball,
						ball.CollisionEvent.HitTime, direction:false, lateral:true, rigid:false);

				case ColliderType.Circle:
					return colliders.Circle(colliderId).HitTest(ref newCollEvent, ref InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Gate:
					return colliders.Gate(colliderId).HitTest(ref newCollEvent, ref InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Line:
					ref var lineCollider = ref colliders.Line(colliderId);
					if (lineCollider.ItemType == ItemType.Trigger) {
						return colliders.Line(colliderId).HitTestBasic(ref newCollEvent, ref InsideOfs, in ball,
							ball.CollisionEvent.HitTime, false, false, false);
					}
					return colliders.Line(colliderId).HitTest(ref newCollEvent, ref InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.LineZ:
					return colliders.LineZ(colliderId).HitTest(ref newCollEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Line3D:
					return colliders.Line3D(colliderId).HitTest(ref newCollEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.LineSlingShot:
					return colliders.LineSlingShot(colliderId).HitTest(ref newCollEvent, ref InsideOfs, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Point:
					return colliders.Point(colliderId).HitTest(ref newCollEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Plane:
					return colliders.Plane(colliderId).HitTest(ref newCollEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Spinner:
					return colliders.Spinner(colliderId).HitTest(ref newCollEvent, ref InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Triangle:
					return colliders.Triangle(colliderId).HitTest(ref newCollEvent, in InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.KickerCircle:
				case ColliderType.TriggerCircle:
					return colliders.Circle(colliderId).HitTestBasicRadius(ref newCollEvent, ref InsideOfs, in ball,
						ball.CollisionEvent.HitTime, false, false, false);

				case ColliderType.Flipper:
					ref var flipperState = ref GetFlipperState(colliderId, ref colliders);
					ref var flipperCollider = ref colliders.Flipper(colliderId);
					return flipperCollider.HitTest(ref newCollEvent, ref InsideOfs, ref flipperState.Hit,
						in flipperState.Movement, in flipperState.Tricks, in flipperState.Static, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Plunger:
					ref var plungerState = ref GetPlungerState(colliderId, ref colliders);
					return colliders.Plunger(colliderId).HitTest(ref newCollEvent, ref InsideOfs, ref plungerState.Movement,
						in plungerState.Collider, in plungerState.Static, in ball, ball.CollisionEvent.HitTime);
			}
			return -1f;
		}

		private bool IsInactiveDropTarget(ref NativeColliders colliders, int colliderId)
		{
			if (colliders.GetItemType(colliderId) == ItemType.HitTarget && HasDropTargetState(colliderId, ref colliders)) {
				ref var dropTargetState = ref GetDropTargetState(colliderId, ref colliders);
				if (dropTargetState.Animation.IsDropped || dropTargetState.Animation.MoveAnimation) {  // QUICKFIX so that DT is not triggered twice
					return true;
				}
			}
			return false;
		}

		#endregion
	}
}
