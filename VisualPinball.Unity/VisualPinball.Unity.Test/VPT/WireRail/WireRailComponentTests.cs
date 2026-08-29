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
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace VisualPinball.Unity.Test
{
	public class WireRailComponentTests
	{
		[TearDown]
		public void TearDown()
		{
			Undo.ClearAll();
		}

		[Test]
		public void ShouldCreateAThreeDimensionalVpxSpline()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var container = component.SplineContainer;

				Assert.That(container.transform.parent, Is.EqualTo(go.transform));
				Assert.That(container.Spline.Count, Is.EqualTo(2));
				Assert.That(container.Spline[0].Position, Is.EqualTo(float3.zero));
				Assert.That(container.Spline[1].Position,
					Is.EqualTo(new float3(0f, 500f, 0f)));
				Assert.That(container.transform.localScale,
					Is.EqualTo(Physics.ScaleInvVector));

				var knot = container.Spline[1];
				knot.Position = new float3(125f, 500f, 75f);
				container.Spline.SetKnot(1, knot);
				Assert.That(container.Spline[1].Position,
					Is.EqualTo(new float3(125f, 500f, 75f)));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldCreateFourBallClearanceRailsByDefault()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();

				Assert.That(component.Segments, Has.Count.EqualTo(1));
				AssertOffsets(component.Segments[0],
					new Vector2(-19f, 0f),
					new Vector2(19f, 0f),
					new Vector2(-19f, 44f),
					new Vector2(19f, 44f));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldOnlyRecreateAMissingSplineChildExplicitly()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				Object.DestroyImmediate(component.SplineContainer.gameObject);

				Assert.That(component.SplineContainer, Is.Null);
				Assert.That(go.transform.childCount, Is.Zero);

				var recreated = component.EnsureSplineContainerExists();
				Assert.That(recreated, Is.Not.Null);
				Assert.That(recreated.transform.parent, Is.SameAs(go.transform));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldApplyUsefulLayoutsFromOneThroughFiveRails()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();

				component.SetRailCount(0, 1);
				AssertOffsets(component.Segments[0], Vector2.zero);

				component.SetRailCount(0, 2);
				AssertOffsets(component.Segments[0],
					new Vector2(-19f, 0f), new Vector2(19f, 0f));

				component.SetRailCount(0, 3);
				AssertOffsets(component.Segments[0],
					new Vector2(-19f, 0f), new Vector2(19f, 0f),
					new Vector2(19f, 44f));
				component.SetThirdRailSide(0, WireRailThirdRailSide.Left);
				Assert.That(component.Segments[0].GetRailOffset(2),
					Is.EqualTo(new Vector2(-19f, 44f)));

				component.SetRailCount(0, 4);
				AssertOffsets(component.Segments[0],
					new Vector2(-19f, 0f), new Vector2(19f, 0f),
					new Vector2(-19f, 44f), new Vector2(19f, 44f));

				component.SetRailCount(0, 5);
				Assert.That(component.Segments[0].GetRailOffset(4),
					Is.EqualTo(new Vector2(0f, 52f)));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldDistributeAdditionalRailsAcrossTheTop()
		{
			var offsets = WireRailLayout.CreateDefaultOffsets(7);

			Assert.That(offsets, Has.Length.EqualTo(7));
			Assert.That(offsets[4], Is.EqualTo(new Vector2(-19f, 52f)));
			Assert.That(offsets[5], Is.EqualTo(new Vector2(0f, 52f)));
			Assert.That(offsets[6], Is.EqualTo(new Vector2(19f, 52f)));
		}

		[Test]
		public void ShouldEditSelectedWirePositionsAndDiametersTogether()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetWireProperties(0, new[] { 0, 2 },
					new[] { new Vector2(-22f, 1f), new Vector2(-20f, 46f) },
					new[] { 12f, 10f });

				Assert.That(component.Segments[0].GetRailOffset(0),
					Is.EqualTo(new Vector2(-22f, 1f)));
				Assert.That(component.Segments[0].GetWireDiameter(0), Is.EqualTo(12f));
				Assert.That(component.Segments[0].GetWireDiameter(1), Is.EqualTo(8f));
				Assert.That(component.Segments[0].GetWireDiameter(2), Is.EqualTo(10f));
				Assert.That(component.RenderMesh.bounds.min.x, Is.EqualTo(-28f).Within(0.05f));
				Assert.That(component.ColliderMesh.vertexCount, Is.GreaterThan(0));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepSegmentLayoutsIndependent()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SplineContainer.Spline.Add(
					new BezierKnot(new float3(50f, 750f, 100f)), TangentMode.AutoSmooth);

				component.SetRailCount(0, 2);
				component.SetRailCount(1, 5);
				component.SetRailOffset(1, 4, new Vector2(3f, 61f));

				Assert.That(component.Segments[0].RailCount, Is.EqualTo(2));
				Assert.That(component.Segments[1].RailCount, Is.EqualTo(5));
				Assert.That(component.Segments[1].GetRailOffset(4),
					Is.EqualTo(new Vector2(3f, 61f)));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldCloneTheLayoutWhenAKnotSplitsASegment()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(0, 3);
				component.SetRailOffset(0, 2, new Vector2(23f, 48f));
				component.SetWireDiameter(0, 2, 11f);

				component.SplineContainer.Spline.Insert(1,
					new BezierKnot(new float3(0f, 250f, 25f)), TangentMode.AutoSmooth);

				Assert.That(component.Segments, Has.Count.EqualTo(2));
				Assert.That(component.Segments[0].GetRailOffset(2),
					Is.EqualTo(new Vector2(23f, 48f)));
				Assert.That(component.Segments[1].GetRailOffset(2),
					Is.EqualTo(new Vector2(23f, 48f)));
				Assert.That(component.Segments[0].GetWireDiameter(2), Is.EqualTo(11f));
				Assert.That(component.Segments[1].GetWireDiameter(2), Is.EqualTo(11f));

				component.SetRailOffset(1, 2, new Vector2(-21f, 46f));
				component.SetWireDiameter(1, 2, 7f);
				Assert.That(component.Segments[0].GetRailOffset(2),
					Is.EqualTo(new Vector2(23f, 48f)));
				Assert.That(component.Segments[0].GetWireDiameter(2), Is.EqualTo(11f));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepNeighboringLayoutsWhenInsertingAndRemovingAKnot()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				spline.Add(new BezierKnot(new float3(100f, 1000f, 100f)),
					TangentMode.AutoSmooth);
				component.SetRailCount(0, 2);
				component.SetRailCount(1, 5);

				spline.Insert(1, new BezierKnot(new float3(25f, 250f, 20f)),
					TangentMode.AutoSmooth);

				Assert.That(component.Segments, Has.Count.EqualTo(3));
				Assert.That(component.Segments[0].RailCount, Is.EqualTo(2));
				Assert.That(component.Segments[1].RailCount, Is.EqualTo(2));
				Assert.That(component.Segments[2].RailCount, Is.EqualTo(5));

				spline.RemoveAt(1);

				Assert.That(component.Segments, Has.Count.EqualTo(2));
				Assert.That(component.Segments[0].RailCount, Is.EqualTo(2));
				Assert.That(component.Segments[1].RailCount, Is.EqualTo(5));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldMatchOpenAndClosedSplineSegmentCounts()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				spline.Add(new BezierKnot(new float3(100f, 600f, 50f)),
					TangentMode.AutoSmooth);

				Assert.That(component.Segments, Has.Count.EqualTo(2));
				spline.Closed = true;
				Assert.That(component.Segments, Has.Count.EqualTo(3));
				spline.Closed = false;
				Assert.That(component.Segments, Has.Count.EqualTo(2));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldGenerateVisibleOctagonalWireTubes()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var mesh = component.RenderMesh;

				Assert.That(mesh, Is.Not.Null);
				Assert.That(mesh.vertexCount, Is.GreaterThan(0));
				Assert.That(mesh.normals, Has.Length.EqualTo(mesh.vertexCount));
				Assert.That(mesh.bounds.min.x, Is.EqualTo(-23f).Within(0.05f));
				Assert.That(mesh.bounds.max.x, Is.EqualTo(23f).Within(0.05f));
				Assert.That(component.SplineContainer.GetComponent<MeshFilter>().sharedMesh,
					Is.SameAs(mesh));
				Assert.That(component.SplineContainer.GetComponent<MeshRenderer>(), Is.Not.Null);
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[TestCase(2, 3, false)]
		[TestCase(3, 5, false)]
		[TestCase(4, 7, false)]
		[TestCase(5, 8, true)]
		[TestCase(9, 8, true)]
		public void ShouldCreateOneSelectivelyOpenBallChannel(int railCount,
			int expectedFacets, bool expectedClosed)
		{
			var offsets = WireRailLayout.CreateDefaultOffsets(railCount);

			Assert.That(WireRailChannelProfile.TryCreate(offsets, 4f, 25f,
				out var profile, out var error), Is.True, error);
			Assert.That(profile.Spans, Has.Count.EqualTo(expectedFacets));
			Assert.That(profile.IsClosed, Is.EqualTo(expectedClosed));
			Assert.That(profile.Spans.Count, Is.LessThanOrEqualTo(8));
		}

		[Test]
		public void ShouldFitTheChannelToDifferentWireDiameters()
		{
			var offsets = WireRailLayout.CreateDefaultOffsets(2);
			var radii = new[] { 3f, 6f };

			Assert.That(WireRailChannelProfile.TryCreate(offsets, radii, 25f,
				out var profile, out var error), Is.True, error);
			Assert.That(math.distance(profile.RestingBallCenter, (float2)offsets[0]),
				Is.EqualTo(28f).Within(0.01f));
			Assert.That(math.distance(profile.RestingBallCenter, (float2)offsets[1]),
				Is.EqualTo(31f).Within(0.01f));
		}

		[Test]
		public void ShouldGenerateAChannelColliderInsteadOfPerWireTubes()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();

				Assert.That(component.ColliderMesh, Is.Not.Null);
				Assert.That(component.ColliderMesh.triangles, Has.Length.EqualTo(224 * 3));
				Assert.That(component.ColliderMesh.normals, Is.Empty);
				Assert.That(component.ColliderMesh.uv, Is.Empty);
				Assert.That(component.RenderMesh.vertexCount,
					Is.GreaterThan(component.ColliderMesh.vertexCount));

				component.SetRailCount(0, 2);
				Assert.That(component.ColliderMesh.triangles, Has.Length.EqualTo(96 * 3));
				Assert.That(component, Is.InstanceOf<ICollidableComponent>());
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRebuildGeometryWhenTheSplineMoves()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				var end = spline[1];
				end.Position = new float3(150f, 500f, 100f);

				spline.SetKnot(1, end);

				Assert.That(component.RenderMesh.bounds.max.x, Is.GreaterThan(140f));
				Assert.That(component.ColliderMesh.bounds.max.x, Is.GreaterThan(130f));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldExpandTheChannelMeshIntoVpePhysicsColliders()
		{
			var go = new GameObject("Wire Rail");
			var transforms = new NativeParallelHashMap<int, float4x4>(0, Allocator.Temp);
			var colliders = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var component = go.AddComponent<WireRailComponent>();
				var collidable = (ICollidableComponent)component;

				collidable.GetColliders(null, null, ref colliders, float4x4.identity, 0f);

				Assert.That(collidable.IsCollidable, Is.True);
				Assert.That(colliders.Count, Is.EqualTo(360));
				for (var i = 0; i < colliders.Count; i++) {
					Assert.That(colliders[i], Is.Not.InstanceOf<CircleCollider>(),
						"the channel must not expand into per-wire tube colliders");
				}
			}
			finally {
				colliders.Dispose();
				transforms.Dispose();
				Object.DestroyImmediate(go);
			}
		}

		private static void AssertOffsets(WireRailSegment segment,
			params Vector2[] expected)
		{
			Assert.That(segment.RailCount, Is.EqualTo(expected.Length));
			for (var i = 0; i < expected.Length; i++) {
				Assert.That(segment.GetRailOffset(i), Is.EqualTo(expected[i]));
			}
		}
	}
}
