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
using Unity.Collections;
using Unity.Mathematics;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Base struct common to all colliders.
	/// Dispatches the interface methods to appropriate implementations for the collider type.
	/// </summary>
	public struct Collider
	{
		public const float Tolerance = 1e-6f; // 1e-9f;

		public ColliderHeader Header;

		public int Id => Header.Id;
		public int ItemId => Header.ItemId;
		public ColliderType Type => Header.Type;
		public PhysicsMaterialData Material => Header.Material;
		public float Threshold => Header.Threshold;
		public bool FireEvents => Header.FireEvents;
		public ItemType ItemType => Header.ItemType;

		public static Collider None => new Collider {
			Header = { Type = ColliderType.None }
		};

		public unsafe ColliderBounds Bounds() {
			fixed (Collider* collider = &this) {
				switch (collider->Type) {
					case ColliderType.Bumper:
					case ColliderType.Circle:
					case ColliderType.KickerCircle:
					case ColliderType.TriggerCircle:
						return ((CircleCollider*) collider)->Bounds;
					case ColliderType.Flipper:
						return ((FlipperCollider*) collider)->Bounds;
					case ColliderType.Gate:
						return ((GateCollider*) collider)->Bounds;
					case ColliderType.Line:
						return ((LineCollider*) collider)->Bounds;
					case ColliderType.Line3D:
						return ((Line3DCollider*) collider)->Bounds;
					case ColliderType.LineSlingShot:
						return ((LineSlingshotCollider*) collider)->Bounds;
					case ColliderType.LineZ:
						return ((LineZCollider*) collider)->Bounds;
					case ColliderType.Point:
						return ((PointCollider*) collider)->Bounds;
					case ColliderType.Plane:
						return ((PlaneCollider*) collider)->Bounds;
					case ColliderType.Plunger:
						return ((PlungerCollider*) collider)->Bounds;
					case ColliderType.Spinner:
						return ((SpinnerCollider*) collider)->Bounds;
					case ColliderType.Triangle:
						return ((TriangleCollider*) collider)->Bounds;
					default:
						throw new InvalidOperationException();
				}
			}
		}

		internal static void FireHitEvent(ref BallState ball, ref NativeQueue<EventData>.ParallelWriter events, in ColliderHeader collHeader)
		{
			if (collHeader.FireEvents/* && collHeader.IsEnabled*/) { // todo enabled

				// is this the same place as last event? if same then ignore it
				var posDiff = ball.EventPosition - ball.Position;
				var distLs = math.lengthsq(posDiff);

				// remember last collide position
				ball.EventPosition = ball.Position;

				// hit targets when used with a captured ball have always a too small distance
				var normalDist = collHeader.ItemType == ItemType.HitTarget ? 0.0f : 0.25f; // magic distance

				// must be a new place if only by a little
				if (distLs > normalDist) {
					events.Enqueue(new EventData(EventId.HitEventsHit, collHeader.ItemId, ball.Id, true));
				}
			}
		}

		internal static void Contact(in ColliderHeader collHeader, ref BallState ball, in CollisionEventData collEvent, double hitTime, in float3 gravity)
		{
			BallCollider.HandleStaticContact(ref ball, in collEvent, collHeader.Material.Friction, (float)hitTime, gravity);
		}
	}
}
