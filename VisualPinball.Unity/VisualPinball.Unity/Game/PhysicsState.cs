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

using NativeTrees;
using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	internal struct PhysicsState
	{
		internal PhysicsEnv Env;
		internal NativeOctree<int> Octree;
		internal BlobAssetReference<ColliderBlob> Colliders;
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

		public PhysicsState(ref PhysicsEnv env, ref NativeOctree<int> octree, ref BlobAssetReference<ColliderBlob> colliders,
			ref NativeQueue<EventData>.ParallelWriter eventQueue, ref InsideOfs insideOfs, ref NativeParallelHashMap<int, BallState> balls,
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

		internal Collider GetCollider(int colliderId) => Colliders.Value.Colliders[colliderId].Value;

		internal bool IsColliderActive(int colliderId)
		{
			var collider = GetCollider(colliderId);
			return !DisabledCollisionItems.Contains(collider.ItemId);
		}

		#region States

		internal ref FlipperState GetFlipperState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref FlipperStates.GetValueByRef(collider.ItemId);
		}

		internal ref PlungerState GetPlungerState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref PlungerStates.GetValueByRef(collider.ItemId);
		}

		internal ref SpinnerState GetSpinnerState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref SpinnerStates.GetValueByRef(collider.ItemId);
		}

		internal ref TriggerState GetTriggerState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref TriggerStates.GetValueByRef(collider.ItemId);
		}

		internal ref KickerState GetKickerState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref KickerStates.GetValueByRef(collider.ItemId);
		}

		internal bool HasDropTargetState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return DropTargetStates.ContainsKey(collider.ItemId);
		}

		internal bool HasHitTargetState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return HitTargetStates.ContainsKey(collider.ItemId);
		}

		internal ref DropTargetState GetDropTargetState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref DropTargetStates.GetValueByRef(collider.ItemId);
		}

		internal ref HitTargetState GetHitTargetState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref HitTargetStates.GetValueByRef(collider.ItemId);
		}

		internal ref BumperState GetBumperState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref BumperStates.GetValueByRef(collider.ItemId);
		}

		internal ref GateState GetGateState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref GateStates.GetValueByRef(collider.ItemId);
		}

		internal ref SurfaceState GetSurfaceState(int colliderId)
		{
			var collider = Colliders.Value.Colliders[colliderId].Value;
			return ref SurfaceStates.GetValueByRef(collider.ItemId);
		}

		#endregion

		#region Hit Test

		internal float HitTest(int colliderId, ref BallState ball, ref CollisionEventData collEvent, ref NativeList<ContactBufferElement> contacts, ref PhysicsState state)
		{
			if (IsInactiveDropTarget(colliderId)) {
				return -1f;
			}
			switch (Colliders.GetType(colliderId)) {
				case ColliderType.Bumper:
				case ColliderType.Circle:
					return Colliders.GetCircleCollider(colliderId).HitTest(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Gate:
					return Colliders.GetGateCollider(colliderId).HitTest(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Line:
					return Colliders.GetLineCollider(colliderId).HitTest(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.LineZ:
					return Colliders.GetLineZCollider(colliderId).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Line3D:
					return Colliders.GetLine3DCollider(colliderId).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.LineSlingShot:
					return Colliders.GetLineSlingshotCollider(colliderId).HitTest(ref collEvent, ref state.InsideOfs, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Point:
					return Colliders.GetPointCollider(colliderId).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Plane:
					return Colliders.GetPlaneCollider(colliderId).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Spinner:
					return Colliders.GetSpinnerCollider(colliderId).HitTest(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Triangle:
					return Colliders.GetTriangleCollider(colliderId).HitTest(ref collEvent, in state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.KickerCircle:
				case ColliderType.TriggerCircle:
					return Colliders.GetCircleCollider(colliderId).HitTestBasicRadius(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime, false, false, false);

				case ColliderType.TriggerLine:
					return Colliders.GetLineCollider(colliderId).HitTestBasic(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime, false, false, false);

				case ColliderType.Flipper:
					ref var flipperState = ref state.GetFlipperState(colliderId);
					return Colliders.GetFlipperCollider(colliderId).HitTest(ref collEvent, ref state.InsideOfs, ref flipperState.Hit,
						in flipperState.Movement, in flipperState.Tricks, in flipperState.Static, in ball, collEvent.HitTime);

				case ColliderType.Plunger:
					ref var plungerState = ref state.GetPlungerState(colliderId);
					return Colliders.GetPlungerCollider(colliderId).HitTest(ref collEvent, ref state.InsideOfs, ref plungerState.Movement,
						in plungerState.Collider, in plungerState.Static, in ball, collEvent.HitTime);
			}
			return -1f;
		}

		private bool IsInactiveDropTarget(int colliderId)
		{
			ref var coll = ref Colliders.Value.Colliders[colliderId].Value;
			if (coll.ItemType == ItemType.HitTarget && HasDropTargetState(colliderId)) {
				ref var dropTargetState = ref GetDropTargetState(colliderId);
				if (dropTargetState.Animation.IsDropped || dropTargetState.Animation.MoveAnimation) {  // QUICKFIX so that DT is not triggered twice
					return true;
				}
			}
			return false;
		}

		#endregion
	}
}
