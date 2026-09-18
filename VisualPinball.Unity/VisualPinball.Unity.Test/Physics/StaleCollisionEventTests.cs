// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// A ball that is not hit-tested must not collide with anything. The default collision
	/// event is a hit with collider 0 at time 0, and a ball held in a kicker skips the hit
	/// tests, so a new ball that got captured before its first physics step collided with
	/// whatever collider 0 was. On a table where that was the playfield floor, the ball was
	/// pushed from the trough exit up to playfield level and kicked from there.
	/// </summary>
	public class StaleCollisionEventTests
	{
		[Test]
		public void NewBallStartsWithoutPendingCollision()
		{
			var go = new GameObject("Ball");
			try {
				var ball = go.AddComponent<BallComponent>();
				var state = ball.CreateState();

				Assert.That(state.CollisionEvent.HasCollider(), Is.False);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void FrozenBallIsLeftAloneByTheCollisionPhase()
		{
			var nonTransformable = new NativeParallelHashMap<int, float4x4>(1, Allocator.Persistent);
			var references = new ColliderReference(ref nonTransformable, Allocator.Persistent);
			try {
				// collider 0 is a floor at z = 0, the way a table's playfield plane is
				references.Add(new PlaneCollider(new float3(0f, 0f, 1f), 0f, new ColliderInfo { ItemId = 1, ItemType = ItemType.Playfield }));
				using var colliders = new NativeColliders(ref references, Allocator.Persistent);
				using var balls = new NativeParallelHashMap<int, BallState>(2, Allocator.Persistent);
				var state = new PhysicsState { Colliders = colliders, Balls = balls };

				// a ball held below the playfield, with the collision event a new ball comes with
				var ball = new BallState {
					Id = 7, Radius = 25f, Mass = 1f, IsFrozen = true,
					Position = new float3(769f, 1868f, -83f),
				};
				Assert.That(ball.CollisionEvent.ColliderId, Is.EqualTo(0), "the default event points at collider 0");

				PhysicsStaticCollision.Collide(PhysicsConstants.PhysFactor, ref ball, ref state);
				Assert.That(ball.Position.z, Is.EqualTo(-83f).Within(1e-4f), "a held ball stays where the kicker put it");

				// a stale ball-ball event: another ball dropping onto the held one
				var other = new BallState { Id = 8, Radius = 25f, Mass = 1f, Position = new float3(769f, 1868f, -33f), Velocity = new float3(0f, 0f, -10f) };
				balls.Add(other.Id, other);
				ball.CollisionEvent.SetBallItem(other.Id);
				ball.CollisionEvent.HitTime = 0f;

				PhysicsDynamicCollision.Collide(PhysicsConstants.PhysFactor, ref ball, ref state);

				Assert.That(math.length(ball.Velocity), Is.EqualTo(0f), "the held ball is not bounced");
				Assert.That(balls[other.Id].Velocity.z, Is.EqualTo(-10f).Within(1e-4f), "nor is the other ball");
			} finally {
				references.Dispose();
				nonTransformable.Dispose();
			}
		}
	}
}
