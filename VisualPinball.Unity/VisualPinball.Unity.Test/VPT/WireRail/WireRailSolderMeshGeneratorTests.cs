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

using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	public class WireRailSolderMeshGeneratorTests
	{
		[Test]
		public void ShouldUseTheFixtureThresholdAsASurfaceGap()
		{
			var firstStart = new float3(-5f, 0f, 0f);
			var firstEnd = new float3(5f, 0f, 0f);
			var secondStart = new float3(0f, 2.5f, -5f);
			var secondEnd = new float3(0f, 2.5f, 5f);

			Assert.That(WireRailWireTouchDetector.TryFindTouch(firstStart, firstEnd, 1f,
				secondStart, secondEnd, 1f, 0.49f, out _), Is.False);
			Assert.That(WireRailWireTouchDetector.TryFindTouch(firstStart, firstEnd, 1f,
				secondStart, secondEnd, 1f, 0.5f, out var touch), Is.True);
			Assert.That(touch.SurfaceDistance, Is.EqualTo(0.5f).Within(0.0001f));
			Assert.That(math.dot(touch.FirstTangent, touch.SecondTangent),
				Is.EqualTo(0f).Within(0.0001f));
		}

		[Test]
		public void ShouldCreateAStableRandomizedLowPolySolderBlob()
		{
			var touch = new WireRailTouch(new float3(0f), new float3(0f, 2f, 0f),
				new float3(1f, 0f, 0f), new float3(0f, 0f, 1f), 1f, 1f, 0f);
			var first = Generate(123u);
			var repeated = Generate(123u);
			var changed = Generate(124u);
			var doubled = Generate(123u, 2f);

			Assert.That(first.Indices,
				Has.Count.EqualTo(WireRailSolderMeshGenerator.TrianglesPerBlob * 3));
			Assert.That(first.Vertices,
				Has.Count.EqualTo(WireRailSolderMeshGenerator.TrianglesPerBlob * 3));
			Assert.That(first.Normals, Has.Count.EqualTo(first.Vertices.Count));
			Assert.That(first.Uvs, Has.Count.EqualTo(first.Vertices.Count));
			Assert.That(Vector3.Dot(first.Normals[0], first.Normals[1]),
				Is.LessThan(0.999f),
				"solder faces should use per-vertex normals for smooth shading");
			Assert.That(repeated.Vertices, Is.EqualTo(first.Vertices),
				"the same join must not shimmer between rebuilds");
			Assert.That(changed.Vertices, Is.Not.EqualTo(first.Vertices),
				"different seeds should not produce cloned solder blobs");
			for (var vertexIndex = 0; vertexIndex < first.Vertices.Count; vertexIndex++) {
				var expected = (Vector3)touch.Position
					+ (first.Vertices[vertexIndex] - (Vector3)touch.Position) * 2f;
				Assert.That(Vector3.Distance(doubled.Vertices[vertexIndex], expected),
					Is.LessThan(0.0001f),
					"solder size must uniformly scale the blob around the wire touch");
			}

			(List<Vector3> Vertices, List<Vector3> Normals,
				List<Vector2> Uvs, List<int> Indices) Generate(uint seed,
				float solderSize = WireRailFixture.DefaultSolderSize)
			{
				var vertices = new List<Vector3>();
				var normals = new List<Vector3>();
				var uvs = new List<Vector2>();
				var indices = new List<int>();
				WireRailSolderMeshGenerator.AppendTouch(touch, seed, solderSize,
					vertices, normals, uvs, indices);
				return (vertices, normals, uvs, indices);
			}
		}

		[Test]
		public void ShouldSolderADefaultBraceToAllFourRails()
		{
			const int radialSegments = 10;
			var gameObject = new GameObject("Wire Rail");
			try {
				var component = gameObject.AddComponent<WireRailComponent>();
				component.SetRailOffset(0, 0, new Vector2(-30f, 0f));
				component.SetRailOffset(0, 1, new Vector2(30f, 0f));
				var railTriangleCount = component.RenderMesh.triangles.Length / 3;
				var fixtureIndex = component.AddBraceFixture(250f);
				var defaultVertices = component.RenderMesh.vertices;
				var defaultTriangleCount = component.RenderMesh.triangles.Length;
				var brace = (WireRailBraceFixture)component.Fixtures[fixtureIndex];
				var touches = new List<WireRailTouch>();

				WireRailSolderMeshGenerator.CollectTouches(component.SplineContainer.Spline,
					component.Segments, component.Fixtures, brace, touches);

				Assert.That(touches, Has.Count.EqualTo(4));
				var braceTriangles = WireRailBraceFixture.DefaultRingDensity
					* radialSegments * 2;
				Assert.That(component.RenderMesh.triangles.Length / 3 - railTriangleCount,
					Is.EqualTo(braceTriangles + touches.Count
						* WireRailSolderMeshGenerator.TrianglesPerBlob));

				component.SetFixtureSolderSize(fixtureIndex, 2f);
				Assert.That(component.RenderMesh.triangles.Length,
					Is.EqualTo(defaultTriangleCount),
					"changing solder size must preserve the generated mesh topology");
				Assert.That(component.RenderMesh.vertices, Is.Not.EqualTo(defaultVertices),
					"the component render path must pass the authored solder size to the generator");
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ShouldNotSolderContinuousEndpointFixtures()
		{
			var gameObject = new GameObject("Wire Rail");
			try {
				var component = gameObject.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				component.AddDropFixture(WireRailEndpoint.Start);
				component.AddDropLoopFixture(WireRailEndpoint.End);
				var touches = new List<WireRailTouch>();
				foreach (var fixture in component.Fixtures) {
					touches.Clear();
					WireRailSolderMeshGenerator.CollectTouches(
						component.SplineContainer.Spline, component.Segments,
						component.Fixtures, fixture, touches);
					Assert.That(touches, Is.Empty,
						$"{fixture.GetType().Name} continues the rail wire and needs no solder");
				}
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void ShouldStoreAndDuplicateTheSolderSettingsPerFixture()
		{
			var gameObject = new GameObject("Wire Rail");
			try {
				var component = gameObject.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddBraceFixture(250f);
				component.SetFixtureSolderThreshold(fixtureIndex, 3.5f);
				component.SetFixtureSolderSize(fixtureIndex, 2.25f);
				var duplicateIndex = component.DuplicateBraceFixture(fixtureIndex);

				Assert.That(component.Fixtures[fixtureIndex].SolderThreshold,
					Is.EqualTo(3.5f));
				Assert.That(component.Fixtures[duplicateIndex].SolderThreshold,
					Is.EqualTo(3.5f));
				Assert.That(component.Fixtures[fixtureIndex].SolderSize,
					Is.EqualTo(2.25f));
				Assert.That(component.Fixtures[duplicateIndex].SolderSize,
					Is.EqualTo(2.25f));
				Assert.Throws<System.ArgumentOutOfRangeException>(() =>
					component.SetFixtureSolderThreshold(fixtureIndex, -0.1f));
				Assert.Throws<System.ArgumentOutOfRangeException>(() =>
					component.SetFixtureSolderSize(fixtureIndex, 0f));
			} finally {
				Object.DestroyImmediate(gameObject);
			}
		}
	}
}
