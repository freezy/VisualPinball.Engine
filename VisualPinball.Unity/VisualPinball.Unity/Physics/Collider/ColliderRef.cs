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

using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public readonly ref struct ColliderRef
	{
		internal readonly int Id;
		private readonly BlobAssetReference<ColliderBlob> _colliders;

		internal ColliderRef(int id, ref BlobAssetReference<ColliderBlob> colliders)
		{
			_colliders = colliders;
			Id = id;
		}

		internal float HitTest(ref BallData ball, ref CollisionEventData collEvent, ref NativeList<ContactBufferElement> contacts, ref PhysicsState state)
		{
			if (IsInactiveDropTarget(ref state)) {
				return -1f;
			}
			switch (_colliders.GetType(Id)) {
				case ColliderType.Bumper:
				case ColliderType.Circle:
					return _colliders.GetCircleCollider(Id).HitTest(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Gate:
					return _colliders.GetGateCollider(Id).HitTest(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Line:
					return _colliders.GetLineCollider(Id).HitTest(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.LineZ:
					return _colliders.GetLineZCollider(Id).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Line3D:
					return _colliders.GetLine3DCollider(Id).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.LineSlingShot:
					return _colliders.GetLineSlingshotCollider(Id).HitTest(ref collEvent, ref state.InsideOfs, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Point:
					return _colliders.GetPointCollider(Id).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Plane:
					return _colliders.GetPlaneCollider(Id).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);

				case ColliderType.Spinner:
					return _colliders.GetSpinnerCollider(Id).HitTest(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.Triangle:
					return _colliders.GetTriangleCollider(Id).HitTest(ref collEvent, in state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime);

				case ColliderType.KickerCircle:
				case ColliderType.TriggerCircle:
					return _colliders.GetCircleCollider(Id).HitTestBasicRadius(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime, false, false, false);

				case ColliderType.TriggerLine:
					return _colliders.GetLineCollider(Id).HitTestBasic(ref collEvent, ref state.InsideOfs, in ball,
						ball.CollisionEvent.HitTime, false, false, false);

				case ColliderType.Flipper:
					ref var flipperState = ref state.GetFlipperState(Id);
					return _colliders.GetFlipperCollider(Id).HitTest(ref collEvent, ref state.InsideOfs, ref flipperState.Hit,
						in flipperState.Movement, in flipperState.Tricks, in flipperState.Static, in ball, collEvent.HitTime);

				case ColliderType.Plunger:
					ref var plungerState = ref state.GetPlungerState(Id);
					return _colliders.GetPlungerCollider(Id).HitTest(ref collEvent, ref state.InsideOfs, ref plungerState.Movement,
						in plungerState.Collider, in plungerState.Static, in ball, collEvent.HitTime);
			}
			return -1f;
		}

		private bool IsInactiveDropTarget(ref PhysicsState state)
		{
			ref var coll = ref _colliders.Value.Colliders[Id].Value;
			if (coll.ItemType == ItemType.HitTarget && state.HasDropTargetState(Id)) {
				ref var dropTargetState = ref state.GetDropTargetState(Id);
				if (dropTargetState.Animation.IsDropped || dropTargetState.Animation.MoveAnimation) {  // QUICKFIX so that DT is not triggered twice
					return true;
				}
			}
			return false;
		}
	}
}
