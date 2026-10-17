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

using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// The polygon of a trigger is not solid: the ball passes through it, and it
	/// only reports when the ball crosses it.
	/// </summary>
	public class TriggerPolygonTests
	{
		private const int TriggerId = 7;
		private const float TopZ = 20f;

		[Test]
		public void BallOnTopOfTheTriggerMakesNoContact()
		{
			var collider = CreateTriggerTop();
			var insideOfs = new InsideOfs(Allocator.Temp);
			try {
				insideOfs.SetInsideOf(TriggerId, 1);
				var ball = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(-50f, -50f, TopZ + 25f) };
				var collEvent = new CollisionEventData();

				var hitTime = collider.HitTest(ref collEvent, in insideOfs, in ball, 1f);

				Assert.That(hitTime, Is.EqualTo(-1f), "a ball resting on the trigger's top is already inside, nothing to report");
				Assert.That(collEvent.IsContact, Is.False, "the trigger's top must not hold the ball up");
			} finally {
				insideOfs.Dispose();
			}
		}

		[Test]
		public void BallTouchingTheTopFromOutsideIsAHitNotAContact()
		{
			var collider = CreateTriggerTop();
			var insideOfs = new InsideOfs(Allocator.Temp);
			try {
				var ball = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(-50f, -50f, TopZ + 25f) };
				var collEvent = new CollisionEventData();

				var hitTime = collider.HitTest(ref collEvent, in insideOfs, in ball, 1f);

				Assert.That(hitTime, Is.EqualTo(0f));
				Assert.That(collEvent.HitFlag, Is.False, "entering the volume is a hit");
				Assert.That(collEvent.IsContact, Is.False);
			} finally {
				insideOfs.Dispose();
			}
		}

		[Test]
		public void BallRollingBelowTheTopIsIgnored()
		{
			var collider = CreateTriggerTop();
			var insideOfs = new InsideOfs(Allocator.Temp);
			try {
				var ball = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(-50f, -50f, 25f), Velocity = new float3(0f, 2f, 0f) };
				var collEvent = new CollisionEventData();

				Assert.That(collider.HitTest(ref collEvent, in insideOfs, in ball, 1f), Is.EqualTo(-1f), "outside of the trigger");
				insideOfs.SetInsideOf(TriggerId, 1);
				Assert.That(collider.HitTest(ref collEvent, in insideOfs, in ball, 1f), Is.EqualTo(-1f), "inside of the trigger");
				Assert.That(collEvent.IsContact, Is.False);
			} finally {
				insideOfs.Dispose();
			}
		}

		[Test]
		public void BallDroppingThroughTheTopIsAHit()
		{
			var collider = CreateTriggerTop();
			var insideOfs = new InsideOfs(Allocator.Temp);
			try {
				// 5 units above the top, falling at 10 units per step
				var ball = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(-50f, -50f, TopZ + 30f), Velocity = new float3(0f, 0f, -10f) };
				var collEvent = new CollisionEventData();

				var hitTime = collider.HitTest(ref collEvent, in insideOfs, in ball, 1f);

				Assert.That(hitTime, Is.EqualTo(0.5f).Within(1e-5f));
				Assert.That(collEvent.HitFlag, Is.False, "entering the volume is a hit");
				Assert.That(collEvent.IsContact, Is.False);
			} finally {
				insideOfs.Dispose();
			}
		}

		[Test]
		public void BallLeavingThroughTheTopIsAnUnHit()
		{
			var collider = CreateTriggerTop();
			var insideOfs = new InsideOfs(Allocator.Temp);
			try {
				insideOfs.SetInsideOf(TriggerId, 1);
				// 5 units below the top, rising at 10 units per step
				var ball = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(-50f, -50f, TopZ + 20f), Velocity = new float3(0f, 0f, 10f) };
				var collEvent = new CollisionEventData();

				var hitTime = collider.HitTest(ref collEvent, in insideOfs, in ball, 1f);

				Assert.That(hitTime, Is.EqualTo(0.5f).Within(1e-5f));
				Assert.That(collEvent.HitFlag, Is.True, "leaving the volume is an unhit");
				Assert.That(collEvent.IsContact, Is.False);
			} finally {
				insideOfs.Dispose();
			}
		}

		[Test]
		public void BallDroppingOntoASeamOfThePolygonIsAHit()
		{
			// a polygon triangulated into two triangles: rounding must not make both reject the shared edge
			var a = new float3(-22.8f, 164.4f, TopZ);
			var b = new float3(224.4f, 132f, TopZ);
			var c = new float3(237.6f, 229.6f, TopZ);
			var d = new float3(-178.4f, 224.8f, TopZ);
			var info = new ColliderInfo { ItemId = TriggerId, ItemType = ItemType.Trigger };
			var triangles = new[] { new TriangleCollider(a, d, b, info), new TriangleCollider(b, d, c, info) };
			var seam = (b + d) * 0.5f;

			var insideOfs = new InsideOfs(Allocator.Temp);
			try {
				var ball = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(seam.x, seam.y, TopZ + 30f), Velocity = new float3(0f, 0f, -10f) };
				var hits = 0;
				foreach (var triangle in triangles) {
					Assert.That(triangle.Normal().z, Is.GreaterThan(0.99f));
					var collEvent = new CollisionEventData();
					if (triangle.HitTest(ref collEvent, in insideOfs, in ball, 1f) >= 0) {
						hits++;
					}
				}
				Assert.That(hits, Is.GreaterThan(0), "a crossing on the seam of two triangles is reported");
			} finally {
				insideOfs.Dispose();
			}
		}

		[Test]
		public void BallCrossingJustOutsideTheEdgeOfATriangleIsAHit()
		{
			// a hair beyond the hypotenuse of the triangle, as rounding would put a point of the neighbor's edge
			var collider = CreateTriggerTop();
			var insideOfs = new InsideOfs(Allocator.Temp);
			try {
				var ball = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(0.0005f, 0.0005f, TopZ + 30f), Velocity = new float3(0f, 0f, -10f) };
				var collEvent = new CollisionEventData();

				Assert.That(collider.HitTest(ref collEvent, in insideOfs, in ball, 1f), Is.EqualTo(0.5f).Within(1e-5f));
			} finally {
				insideOfs.Dispose();
			}
		}

		[Test]
		public void GeneratedPolygonHasNoSolidEdgesAndReportsACrossing()
		{
			var go = new GameObject("trigger");
			var nonTransformable = new NativeParallelHashMap<int, float4x4>(1, Allocator.Persistent);
			var references = new ColliderReference(ref nonTransformable, Allocator.Persistent);
			var insideOfs = new InsideOfs(Allocator.Temp);
			try {
				var trigger = go.AddComponent<TriggerComponent>();
				var triggerCollider = go.AddComponent<TriggerColliderComponent>();
				triggerCollider.HitHeight = TopZ;
				trigger.DragPoints = new[] {
					new DragPointData(0f, 0f), new DragPointData(100f, 0f),
					new DragPointData(100f, 100f), new DragPointData(0f, 100f),
				};
				var api = new TriggerApi(go, null, null);
				new TriggerColliderGenerator(api, trigger, triggerCollider, null, float4x4.identity).GenerateColliders(ref references);

				Assert.That(references.Line3DColliders.Length, Is.EqualTo(0), "no solid edges on the polygon");
				Assert.That(references.PointColliders.Length, Is.EqualTo(0), "no solid corners on the polygon");
				Assert.That(references.LineColliders.Length, Is.EqualTo(4), "one wall per side");
				Assert.That(references.TriangleColliders.Length, Is.GreaterThanOrEqualTo(2));

				// a ball falling onto the middle of the polygon, which is on the seam of the triangles
				var ball = new BallState { Id = 1, Mass = 1f, Radius = 25f, Position = new float3(50f, 50f, TopZ + 30f), Velocity = new float3(0f, 0f, -10f) };
				var hits = 0;
				for (var i = 0; i < references.TriangleColliders.Length; i++) {
					var triangle = references.TriangleColliders[i];
					Assert.That(triangle.Normal().z, Is.GreaterThan(0.99f), "the polygon faces up");
					var collEvent = new CollisionEventData();
					if (triangle.HitTest(ref collEvent, in insideOfs, in ball, 1f) >= 0) {
						Assert.That(collEvent.HitFlag, Is.False, "entering the volume is a hit");
						Assert.That(collEvent.IsContact, Is.False);
						hits++;
					}
				}
				Assert.That(hits, Is.GreaterThan(0), "the crossing is reported");
			} finally {
				insideOfs.Dispose();
				references.Dispose();
				nonTransformable.Dispose();
				Object.DestroyImmediate(go);
			}
		}

		private static TriangleCollider CreateTriggerTop()
		{
			var collider = new TriangleCollider(new float3(-100f, -100f, TopZ), new float3(-100f, 100f, TopZ),
				new float3(100f, -100f, TopZ), new ColliderInfo { ItemId = TriggerId, ItemType = ItemType.Trigger });
			Assert.That(collider.Normal().z, Is.GreaterThan(0.99f));
			return collider;
		}
	}
}
