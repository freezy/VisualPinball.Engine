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
			var hitTime = -1f;
			switch (_colliders.GetType(Id)) {
				case ColliderType.Plane:
					hitTime = _colliders.GetPlaneCollider(Id).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);
					break;
				case ColliderType.Line:
					hitTime = _colliders.GetLineCollider(Id).HitTest(ref collEvent, ref state.InsideOfs, ref ball, ball.CollisionEvent.HitTime);
					break;
				case ColliderType.Triangle:
					hitTime = _colliders.GetTriangleCollider(Id).HitTest(ref collEvent, in state.InsideOfs, in ball, ball.CollisionEvent.HitTime);
					break;
				case ColliderType.Line3D:
					hitTime = _colliders.GetLine3DCollider(Id).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);
					break;
				case ColliderType.Point:
					hitTime = _colliders.GetPointCollider(Id).HitTest(ref collEvent, in ball, ball.CollisionEvent.HitTime);
					break;
				case ColliderType.Flipper:
					var flipperState = state.GetFlipperState(collEvent.ColliderId);
					hitTime = _colliders.GetFlipperCollider(Id).HitTest(
						ref collEvent, ref state.InsideOfs, ref flipperState.Hit,
						in flipperState.Movement, in flipperState.Tricks, in flipperState.Static,
						in ball, collEvent.HitTime
					);
					state.SetFlipperState(collEvent.ColliderId, flipperState);
					break;
			}
			return hitTime;
		}
	}
}
