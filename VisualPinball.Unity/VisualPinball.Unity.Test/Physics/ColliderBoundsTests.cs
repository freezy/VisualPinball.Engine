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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// Collider bounds must contain everything the hit test can report, since the
	/// broad phase only inflates the ball by its radius and the searched motion.
	/// </summary>
	public class ColliderBoundsTests
	{
		[TestCase(121f, 60f)]     // right flipper
		[TestCase(-121f, -60f)]   // left flipper
		[TestCase(170f, 200f)]    // sweep across 180 degrees
		[TestCase(30f, 30f)]      // no sweep
		public void FlipperBoundsContainBaseAndEndCircleAcrossTheSweep(float startAngle, float endAngle)
		{
			const float flipperRadius = 130f;
			const float baseRadius = 21.5f;
			const float endRadius = 13f;
			var aabb = FlipperCollider.ComputeSweepBounds(flipperRadius, baseRadius, endRadius, startAngle, endAngle, 0f, 50f);

			// base circle
			Assert.That(aabb.Left, Is.LessThanOrEqualTo(-baseRadius));
			Assert.That(aabb.Right, Is.GreaterThanOrEqualTo(baseRadius));
			Assert.That(aabb.Top, Is.LessThanOrEqualTo(-baseRadius));
			Assert.That(aabb.Bottom, Is.GreaterThanOrEqualTo(baseRadius));
			Assert.That(aabb.ZLow, Is.EqualTo(0f));
			Assert.That(aabb.ZHigh, Is.EqualTo(50f));

			// end circle at every degree of the sweep
			var from = math.min(startAngle, endAngle);
			var to = math.max(startAngle, endAngle);
			for (var deg = from; deg <= to; deg += 1f) {
				var a = math.radians(deg);
				var center = new float2(math.sin(a), -math.cos(a)) * flipperRadius;
				Assert.That(aabb.Left, Is.LessThanOrEqualTo(center.x - endRadius), $"left at {deg} deg");
				Assert.That(aabb.Right, Is.GreaterThanOrEqualTo(center.x + endRadius), $"right at {deg} deg");
				Assert.That(aabb.Top, Is.LessThanOrEqualTo(center.y - endRadius), $"top at {deg} deg");
				Assert.That(aabb.Bottom, Is.GreaterThanOrEqualTo(center.y + endRadius), $"bottom at {deg} deg");
			}

			// and not absurdly larger than the reach
			var reach = flipperRadius + endRadius + 1f;
			Assert.That(aabb.Width, Is.LessThanOrEqualTo(2f * reach));
			Assert.That(aabb.Height, Is.LessThanOrEqualTo(2f * reach));
		}

		[TestCase(ColliderType.KickerCircle, ItemType.Kicker)]
		[TestCase(ColliderType.TriggerCircle, ItemType.Trigger)]
		public void KickerAndTriggerCircleBoundsCoverTheSphereCap(ColliderType type, ItemType itemType)
		{
			// a large round trigger, hit-tested against a sphere of 2.6 r centered
			// 2.4 r below the top: the cap reaches 0.2 r above the cylinder
			const float radius = 200f;
			var center = new float2(500f, 500f);
			var info = new ColliderInfo { ItemId = 1, ItemType = itemType };
			var circle = new CircleCollider(center, radius, 0f, 50f, info, type);
			Assert.That(circle.Bounds.Aabb.ZHigh, Is.EqualTo(50f + 0.2f * radius).Within(1e-4f));
			Assert.That(new CircleCollider(center, radius, 0f, 50f, info).Bounds.Aabb.ZHigh, Is.EqualTo(50f).Within(1e-4f), "plain circles keep their cylinder");

			// a ball above the top, moving horizontally into the cap: the narrow phase
			// reports a hit within the searched time, so the swept bounds must overlap
			var insideOfs = new InsideOfs(Allocator.Temp);
			try {
				var ball = new BallState { Id = 7, Radius = 25f, Position = new float3(605f, 500f, 80f), Velocity = new float3(-60f, 0f, 0f) };
				var collEvent = new CollisionEventData();
				var hitTime = circle.HitTestBasicRadius(ref collEvent, ref insideOfs, in ball, 0.1f, false, false, false);
				Assert.That(hitTime, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(0.1f), "the cap is hit within the search window");
				Assert.That(circle.Bounds.Aabb.IntersectRect(ball.GetSweptAabb(0.1f)), Is.True, "bounds must admit the ball the hit test can hit");
			} finally {
				insideOfs.Dispose();
			}
		}

		[Test]
		public void PlungerBoundsFollowThePlungerHeight()
		{
			var go = new GameObject("plunger");
			try {
				var plunger = go.AddComponent<PlungerComponent>();
				var collider = go.AddComponent<PlungerColliderComponent>();
				plunger.Position = new Vector3(450f, 2000f, 100f);
				var info = new ColliderInfo { ItemId = 1, ItemType = ItemType.Plunger };
				var plungerCollider = new PlungerCollider(plunger, collider, info);
				Assert.That(plungerCollider.Bounds.Aabb.ZLow, Is.EqualTo(100f).Within(1e-3f));
				Assert.That(plungerCollider.Bounds.Aabb.ZHigh, Is.EqualTo(100f + Plunger.PlungerHeight).Within(1e-3f));
				Assert.That(plungerCollider.LineSegEnd.ZLow, Is.EqualTo(plungerCollider.Bounds.Aabb.ZLow).Within(1e-3f), "line colliders and bounds share the height range");
				Assert.That(plungerCollider.LineSegEnd.ZHigh, Is.EqualTo(plungerCollider.Bounds.Aabb.ZHigh).Within(1e-3f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void MeshColliderGenerationSkipsDegenerateTriangles()
		{
			var nonTransformable = new NativeParallelHashMap<int, float4x4>(1, Allocator.Persistent);
			var references = new ColliderReference(ref nonTransformable, Allocator.Persistent);
			var vertices = new NativeArray<Vector3>(new[] {
				new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(0f, 100f, 0f),
				new Vector3(0f, 0f, 50f), new Vector3(0f, 0f, 100f), // collinear with vertex 0
			}, Allocator.Persistent);
			var indices = new NativeArray<int>(new[] { 0, 1, 2, 0, 3, 4 }, Allocator.Persistent);
			try {
				var info = new ColliderInfo { ItemId = 1, ItemType = ItemType.Primitive };
				ColliderUtils.GenerateCollidersFromMesh(in vertices, in indices, float4x4.identity, info, ref references, true);
				Assert.That(references.Count, Is.EqualTo(1), "only the proper triangle gets a collider");
				Assert.That(ColliderUtils.IsDegenerate(vertices[0], vertices[3], vertices[4]), Is.True);
				Assert.That(ColliderUtils.IsDegenerate(vertices[0], vertices[1], vertices[2]), Is.False);

				// the test is scale independent: a 2 mm triangle in meters is a valid triangle,
				// a collinear one in meters is not, and a zero-length edge is degenerate
				var m = new float3(0.1f, 0.2f, 0f);
				Assert.That(ColliderUtils.IsDegenerate(m, m + new float3(0.002f, 0f, 0f), m + new float3(0f, 0.002f, 0f)), Is.False);
				Assert.That(ColliderUtils.IsDegenerate(m, m + new float3(0.002f, 0f, 0f), m + new float3(0.004f, 0f, 0f)), Is.True);
				Assert.That(ColliderUtils.IsDegenerate(m, m, m + new float3(0f, 0.002f, 0f)), Is.True);
			} finally {
				indices.Dispose();
				vertices.Dispose();
				references.Dispose();
				nonTransformable.Dispose();
			}
		}
	}
}
