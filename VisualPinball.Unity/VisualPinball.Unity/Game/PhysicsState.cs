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
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	internal struct PhysicsState
	{
		internal PhysicsEnv Env;
		internal NativeOctree<int> Octree;
		internal NativeColliders Colliders;
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

		internal ref ColliderHeader GetColliderHeader(int colliderId) => ref Colliders.GetHeader(colliderId);
		internal ColliderType GetColliderType(int colliderId) => Colliders.GetHeader(colliderId).Type;

		internal bool IsColliderActive(int colliderId) => !DisabledCollisionItems.Contains(Colliders.GetItemId(colliderId));

		#region States

		internal ref FlipperState GetFlipperState(int colliderId) => ref FlipperStates.GetValueByRef(Colliders.GetItemId(colliderId));

		internal ref PlungerState GetPlungerState(int colliderId) => ref PlungerStates.GetValueByRef(Colliders.GetItemId(colliderId));

		internal ref SpinnerState GetSpinnerState(int colliderId) => ref SpinnerStates.GetValueByRef(Colliders.GetItemId(colliderId));

		internal ref TriggerState GetTriggerState(int colliderId) => ref TriggerStates.GetValueByRef(Colliders.GetItemId(colliderId));

		internal ref KickerState GetKickerState(int colliderId) => ref KickerStates.GetValueByRef(Colliders.GetItemId(colliderId));


		internal bool HasDropTargetState(int colliderId) => DropTargetStates.ContainsKey(Colliders.GetItemId(colliderId));

		internal bool HasHitTargetState(int colliderId) => HitTargetStates.ContainsKey(Colliders.GetItemId(colliderId));

		internal ref DropTargetState GetDropTargetState(int colliderId) => ref DropTargetStates.GetValueByRef(Colliders.GetItemId(colliderId));

		internal ref HitTargetState GetHitTargetState(int colliderId) => ref HitTargetStates.GetValueByRef(Colliders.GetItemId(colliderId));

		internal ref BumperState GetBumperState(int colliderId) => ref BumperStates.GetValueByRef(Colliders.GetItemId(colliderId));

		internal ref GateState GetGateState(int colliderId) => ref GateStates.GetValueByRef(Colliders.GetItemId(colliderId));

		internal ref SurfaceState GetSurfaceState(int colliderId) => ref SurfaceStates.GetValueByRef(Colliders.GetItemId(colliderId));

		#endregion

		#region Hit Test

		internal float HitTest(int colliderId, ref BallState ball, ref CollisionEventData collEvent, ref NativeList<ContactBufferElement> contacts, ref PhysicsState state)
		{
			if (IsInactiveDropTarget(colliderId)) {
				return -1f;
			}
			switch (GetColliderType(colliderId)) {
				case ColliderType.Bumper:
				case ColliderType.Circle:
					return Colliders.Circle(colliderId).HitTest(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Gate:
					return Colliders.Gate(colliderId).HitTest(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Line:
					return Colliders.Line(colliderId).HitTest(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.LineZ:
					return Colliders.LineZ(colliderId).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Line3D:
					return Colliders.Line3D(colliderId).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.LineSlingShot:
					return Colliders.LineSlingShot(colliderId).HitTest(ref collEvent, ref state.InsideOfs, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Point:
					return Colliders.Point(colliderId).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Plane:
					return Colliders.Plane(colliderId).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Spinner:
					return Colliders.Spinner(colliderId).HitTest(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Triangle:
					return Colliders.Triangle(colliderId).HitTest(ref collEvent, in state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.KickerCircle:
				case ColliderType.TriggerCircle:
					return Colliders.Circle(colliderId).HitTestBasicRadius(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime, false, false, false);

				case ColliderType.TriggerLine:
					return Colliders.Line(colliderId).HitTestBasic(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime, false, false, false);

				case ColliderType.Flipper:
					ref var flipperState = ref state.GetFlipperState(colliderId);
					return Colliders.Flipper(colliderId).HitTest(ref collEvent, ref state.InsideOfs, ref flipperState.Hit,
						in flipperState.Movement, in flipperState.Tricks, in flipperState.Static, in ball, collEvent.HitTime);

				case ColliderType.Plunger:
					ref var plungerState = ref state.GetPlungerState(colliderId);
					return Colliders.Plunger(colliderId).HitTest(ref collEvent, ref state.InsideOfs, ref plungerState.Movement,
						in plungerState.Collider, in plungerState.Static, in ball, collEvent.HitTime);
			}
			return -1f;
		}

		private bool IsInactiveDropTarget(int colliderId)
		{
			if (Colliders.GetItemType(colliderId) == ItemType.HitTarget && HasDropTargetState(colliderId)) {
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
