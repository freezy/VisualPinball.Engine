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

// ReSharper disable ConvertIfStatementToSwitchStatement

using Unity.Profiling;
using VisualPinball.Unity;

namespace VisualPinballUnity
{
	internal static class PhysicsStaticCollision
	{
		private static readonly ProfilerMarker PerfMarker = new("PhysicsStaticCollision");

		internal static void Collide(float hitTime, ref BallData ball, uint timeMs, ref PhysicsState state)
		{
			
			// find balls with hit objects and minimum time
			if (ball.CollisionEvent.ColliderId < 0 || ball.CollisionEvent.HitTime > hitTime) {
				return;
			}

			PerfMarker.Begin();

			Collide(ref ball, timeMs, ref state);

			// remove trial hit object pointer
			ball.CollisionEvent.ClearCollider();

			PerfMarker.End();
		}

		private static void Collide(ref BallData ball, uint timeMs, ref PhysicsState state)
		{
			switch (state.Colliders.GetType(ball.CollisionEvent.ColliderId)) {
				case ColliderType.Plane:
					state.Colliders.GetPlaneCollider(ball.CollisionEvent.ColliderId).Collide(ref ball, in ball.CollisionEvent, ref state.Env.Random);
					break;
				case ColliderType.Line:
					state.Colliders.GetLineCollider(ball.CollisionEvent.ColliderId).Collide(ref ball, ref state.EventQueue, ball.Id, in ball.CollisionEvent, ref state.Env.Random);
					break;
				case ColliderType.Triangle:
					state.Colliders.GetTriangleCollider(ball.CollisionEvent.ColliderId).Collide(ref ball, ref state.EventQueue, ball.Id, in ball.CollisionEvent, ref state.Env.Random);
					break;
				case ColliderType.Line3D:
					state.Colliders.GetLine3DCollider(ball.CollisionEvent.ColliderId).Collide(ref ball, ref state.EventQueue, ball.Id, in ball.CollisionEvent, ref state.Env.Random);
					break;
				case ColliderType.Point:
					state.Colliders.GetPointCollider(ball.CollisionEvent.ColliderId).Collide(ref ball, ref state.EventQueue, ball.Id, in ball.CollisionEvent, ref state.Env.Random);
					break;
				case ColliderType.Flipper:
					ref var flipperState = ref state.GetFlipperState(in ball.CollisionEvent);
					state.Colliders.GetFlipperCollider(ball.CollisionEvent.ColliderId).Collide(ref ball, ref ball.CollisionEvent, ref flipperState.Movement,
						ref state.EventQueue, in ball.Id, in flipperState.Tricks, in flipperState.Static,
						in flipperState.Velocity, in flipperState.Hit, timeMs);
					break;
			}
		}
	}
}
