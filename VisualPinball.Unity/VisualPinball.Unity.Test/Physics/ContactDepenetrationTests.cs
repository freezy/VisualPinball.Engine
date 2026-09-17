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
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// A ball that something pressed into the floor must come back out, and a
	/// collider embedded in the ball from above must not push it into the floor.
	/// </summary>
	public class ContactDepenetrationTests
	{
		private static readonly float3 Gravity = new(0f, 0f, -0.8f);

		[Test]
		public void FloorContactRecoversAnEmbeddedBall()
		{
			var ball = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(0f, 0f, 20f) };
			var contact = new CollisionEventData { ColliderId = 3, HitNormal = new float3(0f, 0f, 1f), HitDistance = -5f };

			BallCollider.HandleStaticContact(ref ball, in contact, 0.1f, PhysicsConstants.PhysFactor, in Gravity, float3.zero);

			Assert.That(ball.Position.z, Is.GreaterThan(24.9f), "a sustained floor contact lifts the ball out");
		}

		[Test]
		public void CeilingContactDoesNotShoveTheBallDown()
		{
			// a cover 10 units lower than the ball is tall, embedded from above
			var ball = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(0f, 0f, 25f) };
			var contact = new CollisionEventData { ColliderId = 3, HitNormal = new float3(0f, 0f, -1f), HitDistance = -10f };

			BallCollider.HandleStaticContact(ref ball, in contact, 0.1f, PhysicsConstants.PhysFactor, in Gravity, float3.zero);

			Assert.That(ball.Position.z, Is.EqualTo(25f).Within(1e-4f), "no recovery along gravity");
		}

		[Test]
		public void SlopedSupportContactStillRecovers()
		{
			// a 35 degree ramp: its normal opposes gravity, so the ball still recovers along it
			var normal = math.normalize(new float3(0f, math.sin(math.radians(35f)), math.cos(math.radians(35f))));
			var ball = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(0f, 0f, 25f) };
			var contact = new CollisionEventData { ColliderId = 3, HitNormal = normal, HitDistance = -3f };

			BallCollider.HandleStaticContact(ref ball, in contact, 0.1f, PhysicsConstants.PhysFactor, in Gravity, float3.zero);

			Assert.That(math.dot(ball.Position - new float3(0f, 0f, 25f), normal), Is.GreaterThan(2.9f), "recovered along the ramp normal");
		}

		[Test]
		public void WallContactStillRecoversSideways()
		{
			var ball = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(0f, 0f, 25f) };
			var contact = new CollisionEventData { ColliderId = 3, HitNormal = new float3(1f, 0f, 0f), HitDistance = -2f };

			BallCollider.HandleStaticContact(ref ball, in contact, 0.1f, PhysicsConstants.PhysFactor, in Gravity, float3.zero);

			Assert.That(ball.Position.x, Is.GreaterThan(1.9f), "a wall contact still pushes the ball out of the wall");
		}

		[TestCase(ItemType.Playfield, 25f)]
		[TestCase(ItemType.Primitive, 20f)]
		public void PlayfieldFloorCollisionPushesTheBallAllTheWayOut(ItemType itemType, float expectedZ)
		{
			// floor triangle at z = 0 with an upward normal, large enough to contain the ball
			var collider = new TriangleCollider(new float3(-1000f, -1000f, 0f), new float3(-1000f, 1000f, 0f),
				new float3(1000f, -1000f, 0f), new ColliderInfo { ItemId = 1, ItemType = itemType });
			Assert.That(collider.Normal().z, Is.GreaterThan(0.99f));

			var state = new PhysicsState();
			var events = new NativeQueue<EventData>(Allocator.Temp);
			try {
				var writer = events.AsParallelWriter();
				var collision = new CollisionEventData { ColliderId = 0, HitNormal = new float3(0f, 0f, 1f), HitDistance = -10f };
				var ball = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(0f, 0f, 15f), Velocity = new float3(2f, 0f, -0.5f) };

				collider.Collide(ref ball, ref writer, in collision, ref state);

				// the playfield pushes the ball fully out; other meshes only by DispLimit per impact
				Assert.That(ball.Position.z, Is.EqualTo(expectedZ).Within(0.01f));
				Assert.That(ball.Velocity.z, Is.GreaterThanOrEqualTo(0f), "the impact reflects the downward velocity");
			} finally {
				events.Dispose();
			}
		}

		[Test]
		public void PlayfieldWallTriangleDoesNotPushSidewaysBeyondTheImpactLimit()
		{
			// a vertical playfield triangle (a cutout wall) with a +x normal
			var collider = new TriangleCollider(new float3(0f, -1000f, -100f), new float3(0f, -1000f, 1000f),
				new float3(0f, 1000f, -100f), new ColliderInfo { ItemId = 1, ItemType = ItemType.Playfield });
			Assert.That(collider.Normal().x, Is.GreaterThan(0.99f));

			var state = new PhysicsState();
			var events = new NativeQueue<EventData>(Allocator.Temp);
			try {
				var writer = events.AsParallelWriter();
				var collision = new CollisionEventData { ColliderId = 0, HitNormal = new float3(1f, 0f, 0f), HitDistance = -10f };
				var ball = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(15f, 0f, 25f), Velocity = new float3(-1f, 0f, 0f) };

				collider.Collide(ref ball, ref writer, in collision, ref state);

				Assert.That(ball.Position.x, Is.EqualTo(15f + PhysicsConstants.DispLimit).Within(0.01f));
			} finally {
				events.Dispose();
			}
		}
	}
}
