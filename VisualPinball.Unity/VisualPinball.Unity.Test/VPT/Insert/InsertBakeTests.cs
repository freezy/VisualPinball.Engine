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
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class InsertBakeTests
	{
		private static readonly float Scale = VisualPinball.Unity.Physics.ScaleInv;

		private const float Depth = 20f;
		private const float ChannelDepth = 4f;
		private const float Offset = 1.5f;

		private static (Mesh Mesh, InsertMeshLayout Layout) Generate(float radius, float channel, int bursts,
			float bevel = 0f, bool holes = false, float offset = Offset, InsertHole[] holeList = null)
		{
			var outline = InsertOutlineBuilder.Build(InsertShapes.RectangleCorners(40f, 30f, new InsertCornerSettings(radius, 6)), 1);
			var inset = channel > 0f ? outline.ClampInset(channel) : 0f;
			var body = new InsertBody {
				Depth = Depth,
				ChannelWidth = inset,
				ChannelDepth = channel > 0f ? ChannelDepth : 0f,
				StarburstBursts = bursts,
				StarburstCenter = new float2(0f, -40f),
				StarburstOffset = offset,
				BottomBevel = bevel,
				BottomBevelSegments = 3,
				HoleSegments = 12,
				Holes = holeList ?? (holes ? InsertHoles.AtCorners(outline, inset).Select(c => new InsertHole(c, 2f)).ToArray() : null),
			};
			var layout = new InsertMeshLayout();
			var mesh = InsertMeshGenerator.Generate(outline, body, new Mesh(), layout);
			return (mesh, layout);
		}

		private static InsertBakeRequest Request(Mesh mesh, InsertMeshLayout layout)
			=> InsertBakeRequest.Build(mesh, layout, InsertBakeSettings.Default, Offset);

		[TestCase(4f, 3f, 12, 1f, true)]
		[TestCase(0f, 3f, 0, 0f, false)]
		[TestCase(4f, 0f, 0, 1f, false)]
		public void ShouldLayEachTriangleOutInOneChart(float radius, float channel, int bursts, float bevel, bool holes)
		{
			var (mesh, layout) = Generate(radius, channel, bursts, bevel, holes);
			try {
				Assert.That(layout.VertexCharts, Has.Length.EqualTo(mesh.vertexCount));
				var triangles = mesh.triangles;
				for (var i = 0; i < triangles.Length; i += 3) {
					var chart = layout.VertexCharts[triangles[i]];
					Assert.That(layout.VertexCharts[triangles[i + 1]], Is.EqualTo(chart), $"triangle {i / 3} spans two charts");
					Assert.That(layout.VertexCharts[triangles[i + 2]], Is.EqualTo(chart), $"triangle {i / 3} spans two charts");
				}

			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		[TestCase(12, true)]
		[TestCase(0, false)]
		public void ShouldBakeTheRaisedMiddleFlat(int bursts, bool holes)
		{
			// The flat chart is the raised middle and nothing else: it faces up, and
			// the starburst straddles the flat copy's height by its offset.
			var (mesh, layout) = Generate(4f, 3f, bursts, 1f, holes);
			try {
				Assert.That(layout.FlatHeight, Is.EqualTo((-Depth + ChannelDepth) * Scale).Within(1e-7f));
				var vertices = mesh.vertices;
				var normals = mesh.normals;
				var flat = Enumerable.Range(0, mesh.vertexCount).Where(i => layout.VertexCharts[i] == layout.FlatChart).ToArray();
				Assert.That(flat, Is.Not.Empty);
				var reach = (bursts > 0 ? Offset : 0f) * Scale + 1e-7f;
				foreach (var i in flat) {
					Assert.That(vertices[i].y, Is.InRange(layout.FlatHeight - reach, layout.FlatHeight + reach), $"vertex {i}");
					Assert.That(normals[i].y, Is.GreaterThan(0.5f), $"vertex {i} does not face up");
				}

			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldBakeNothingFlatWithoutAChannel()
		{
			var (mesh, layout) = Generate(4f, 0f, 0, 1f);
			try {
				Assert.That(layout.VertexCharts, Has.No.Member(layout.FlatChart));
				Assert.That(Request(mesh, layout).FlatFaces, Is.Empty);

			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldBakeNothingFlatWhenAHoleTakesTheWholeMiddle()
		{
			var (mesh, layout) = Generate(4f, 3f, 12, 1f, holeList: new[] { new InsertHole(Vector2.zero, 40f) });
			try {
				Assert.That(Request(mesh, layout).FlatFaces, Is.Empty);

			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldLayTheChannelFloorOutInPieces()
		{
			// A whole ring laid out from above takes up the square it goes round.
			// A rounded rectangle's floor turns a full circle, a piece at most a
			// sixth of one, so it comes out as more than four pieces.
			var (mesh, layout) = Generate(6f, 3f, 12, 0f);
			try {
				var vertices = mesh.vertices;
				var normals = mesh.normals;
				var floor = -Depth * Scale;
				var pieces = Enumerable.Range(0, mesh.vertexCount)
					.Where(i => math.abs(vertices[i].y - floor) < 1e-7f && normals[i].y > 0.99f)
					.Select(i => layout.VertexCharts[i])
					.Distinct()
					.Count();
				Assert.That(pieces, Is.GreaterThan(4));

			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldHashTheMeshItself()
		{
			var (a, first) = Generate(4f, 3f, 12, 1f, true);
			var (b, same) = Generate(4f, 3f, 12, 1f, true);
			var (c, other) = Generate(4f, 3f, 12, 1f, true, Offset * 0.5f);
			try {
				Assert.That(same.Hash, Is.EqualTo(first.Hash));
				Assert.That(other.Hash, Is.Not.EqualTo(first.Hash));

			} finally {
				Object.DestroyImmediate(a);
				Object.DestroyImmediate(b);
				Object.DestroyImmediate(c);
			}
		}

		[TestCase(4f, 3f, 12, 1f, true)]
		[TestCase(0f, 0f, 0, 0f, false)]
		public void ShouldWeldByPositionAndKeepCornersInTriangleOrder(float radius, float channel, int bursts, float bevel, bool holes)
		{
			var (mesh, layout) = Generate(radius, channel, bursts, bevel, holes);
			try {
				var request = Request(mesh, layout);
				var vertices = mesh.vertices;
				var normals = mesh.normals;
				var uvs = mesh.uv;
				var triangles = mesh.triangles;

				var distinct = new HashSet<Vector3>(vertices).Count;
				Assert.That(request.Positions, Has.Length.EqualTo(distinct * 3));
				Assert.That(request.Triangles, Has.Length.EqualTo(triangles.Length));
				Assert.That(request.Charts, Has.Length.EqualTo(triangles.Length / 3));
				for (var corner = 0; corner < triangles.Length; corner++) {
					var vertex = triangles[corner];
					var welded = request.Triangles[corner];
					var position = new Vector3(request.Positions[welded * 3], request.Positions[welded * 3 + 1], request.Positions[welded * 3 + 2]);
					Assert.That(position, Is.EqualTo(vertices[vertex]));
					Assert.That(request.Normals[corner * 3 + 1], Is.EqualTo(normals[vertex].y));
					Assert.That(request.Uvs[corner * 2], Is.EqualTo(uvs[vertex].x));
				}
				Assert.That(request.FlatFaces, channel > 0f ? Is.Not.Empty : Is.Empty);
				foreach (var triangle in request.FlatFaces) {
					Assert.That(request.Charts[triangle], Is.EqualTo(layout.FlatChart));
				}

			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		private static InsertBakeData FakeBake(Mesh mesh, Hash128 hash)
		{
			// Every corner its own UV and a tangent flipping from corner to corner,
			// so shared vertices have to split, and a mix-up shows.
			var corners = mesh.triangles.Length;
			var bake = ScriptableObject.CreateInstance<InsertBakeData>();
			bake.Hash = hash.ToString();
			bake.Uvs = Enumerable.Range(0, corners).Select(i => new Vector2(i / (float)corners, (i % 3) / 3f)).ToArray();
			bake.Tangents = Enumerable.Range(0, corners).Select(i => new Vector4(1f, 0f, 0f, i % 2 == 0 ? 1f : -1f)).ToArray();
			return bake;
		}

		[Test]
		public void ShouldApplyTheBakeCornerByCorner()
		{
			var (mesh, layout) = Generate(4f, 3f, 12, 1f, true);
			var bake = FakeBake(mesh, layout.Hash);
			try {
				var vertices = mesh.vertices;
				var normals = mesh.normals;
				var triangles = mesh.triangles;

				Assert.That(bake.TryApply(mesh, layout.Hash), Is.True);
				var applied = mesh.triangles;
				Assert.That(applied, Has.Length.EqualTo(triangles.Length));
				Assert.That(mesh.vertexCount, Is.GreaterThan(vertices.Length));
				var uvs = mesh.uv;
				var tangents = mesh.tangents;
				var appliedVertices = mesh.vertices;
				var appliedNormals = mesh.normals;
				for (var corner = 0; corner < triangles.Length; corner++) {
					var vertex = applied[corner];
					Assert.That(uvs[vertex], Is.EqualTo(bake.Uvs[corner]));
					Assert.That(tangents[vertex], Is.EqualTo(bake.Tangents[corner]));
					Assert.That(appliedVertices[vertex], Is.EqualTo(vertices[triangles[corner]]));
					Assert.That(appliedNormals[vertex], Is.EqualTo(normals[triangles[corner]]));
				}

			} finally {
				Object.DestroyImmediate(mesh);
				Object.DestroyImmediate(bake);
			}
		}

		[Test]
		public void ShouldNotApplyABakeMadeForAnotherMesh()
		{
			var (mesh, layout) = Generate(4f, 3f, 12, 1f, true);
			var (other, otherLayout) = Generate(4f, 3f, 12, 1f, true, Offset * 0.5f);
			var bake = FakeBake(other, otherLayout.Hash);
			try {
				var count = mesh.vertexCount;
				Assert.That(bake.TryApply(mesh, layout.Hash), Is.False);
				Assert.That(mesh.vertexCount, Is.EqualTo(count));
				Assert.That(mesh.tangents, Is.Empty);

			} finally {
				Object.DestroyImmediate(mesh);
				Object.DestroyImmediate(other);
				Object.DestroyImmediate(bake);
			}
		}

		[Test]
		public void ShouldWearTheBakedMaterialOnlyWhileTheBakeFits()
		{
			var go = new GameObject("Insert");
			var insert = go.AddComponent<InsertComponent>();
			var renderer = go.GetComponent<MeshRenderer>();
			var shader = Shader.Find("Hidden/InternalErrorShader");
			var plain = new Material(shader) { name = "plain" };
			var baked = new Material(shader) { name = "baked" };
			var chosen = new Material(shader) { name = "chosen" };
			var probe = new Mesh();
			InsertBakeData bake = null;
			try {
				renderer.sharedMaterial = plain;
				var layout = new InsertMeshLayout();
				insert.Generate(probe, layout);
				bake = FakeBake(probe, layout.Hash);
				bake.Material = baked;

				insert.SetBake(bake);
				Assert.That(insert.IsBakeFresh, Is.True);
				Assert.That(renderer.sharedMaterial, Is.SameAs(baked));
				Assert.That(insert.UnbakedMaterial, Is.SameAs(plain));

				// Any change to the mesh makes the bake stale, and takes its material off.
				var serialized = new UnityEditor.SerializedObject(insert);
				serialized.FindProperty("_depth").floatValue += 1f;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.IsBakeFresh, Is.False);
				Assert.That(renderer.sharedMaterial, Is.SameAs(plain));

				// Changed back, it fits again.
				serialized.FindProperty("_depth").floatValue -= 1f;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.IsBakeFresh, Is.True);
				Assert.That(renderer.sharedMaterial, Is.SameAs(baked));

				// A material chosen by hand is left alone.
				renderer.sharedMaterial = chosen;
				insert.RebuildMesh();
				Assert.That(renderer.sharedMaterial, Is.SameAs(chosen));

				renderer.sharedMaterial = baked;
				insert.SetBake(null);
				Assert.That(insert.Bake, Is.Null);
				Assert.That(renderer.sharedMaterial, Is.SameAs(plain));

			} finally {
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(probe);
				Object.DestroyImmediate(plain);
				Object.DestroyImmediate(baked);
				Object.DestroyImmediate(chosen);
				if (bake != null) {
					Object.DestroyImmediate(bake);
				}
			}
		}
	}
}
