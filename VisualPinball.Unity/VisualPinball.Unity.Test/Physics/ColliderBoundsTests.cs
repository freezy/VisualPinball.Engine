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
using VisualPinball.Engine.VPT;

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
			} finally {
				indices.Dispose();
				vertices.Dispose();
				references.Dispose();
				nonTransformable.Dispose();
			}
		}
	}
}
