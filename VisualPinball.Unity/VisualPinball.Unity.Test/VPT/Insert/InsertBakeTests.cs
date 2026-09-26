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
using System.Reflection;
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

		[Test]
		public void ShouldDrawWallQuadsWithoutTheirTriangleDiagonals()
		{
			var (mesh, layout) = Generate(0f, 0f, 0);
			try {
				var vertices = mesh.vertices;
				var triangles = mesh.triangles;
				var lines = layout.PreviewEdges;
				bool HasEdge(int from, int to) => Enumerable.Range(0, lines.Length / 2).Any(i =>
					lines[i * 2] == vertices[from] && lines[i * 2 + 1] == vertices[to] ||
					lines[i * 2] == vertices[to] && lines[i * 2 + 1] == vertices[from]);
				var a = triangles[0];
				var b = triangles[1];
				var c = triangles[2];
				var d = triangles[5];
				Assert.That(HasEdge(a, b) && HasEdge(b, c) && HasEdge(c, d) && HasEdge(d, a), Is.True,
					"the four sides of the generated wall quad are visible");
				Assert.That(HasEdge(a, c), Is.False, "the triangle diagonal is hidden");
			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldHideHoleBridgesInTheStarburstPreview()
		{
			var center = new float2(5f, 0f);
			var (mesh, layout) = Generate(4f, 3f, 12, 1f,
				holeList: new[] { new InsertHole(center, 2f) });
			try {
				var local = InsertMeshGenerator.ToLocal(center);
				var origin = new float2(local.x, local.z);
				var radius = 2f * Scale;
				float Distance(Vector3 point) => math.distance(new float2(point.x, point.z), origin);
				bool OnHole(Vector3 point) => math.abs(Distance(point) - radius) < 0.02f * Scale;
				var lines = layout.PreviewEdges;
				Assert.That(layout.OpticalStarburst.z, Is.GreaterThan(0f));
				Assert.That(lines.Any(OnHole), Is.True, "the hole contour is present in the preview");
				Assert.That(Enumerable.Range(0, lines.Length / 2).Any(i =>
					(OnHole(lines[i * 2]) && Distance(lines[i * 2 + 1]) > 5f * Scale) ||
					(OnHole(lines[i * 2 + 1]) && Distance(lines[i * 2]) > 5f * Scale)), Is.False,
					"the face-walk bridge from the hole to the island edge is not a visible wire");
			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		[TestCase(0f, 1.5f, 0f, 1.5f)]
		[TestCase(0.25f, 2f, 0.25f, 2f)]
		[TestCase(2f, 0f, 1f, 0.1f)]
		public void ShouldSendNormalGrainInPhysicalUnits(float strength, float size, float expectedStrength, float expectedSize)
		{
			var (mesh, layout) = Generate(3f, 2f, 4);
			try {
				var settings = InsertBakeSettings.Default;
				settings.NormalGrainStrength = strength;
				settings.NormalGrainSize = size;
				var request = InsertBakeRequest.Build(mesh, layout, settings, Offset);
				Assert.That(request.NormalGrainStrength, Is.EqualTo(expectedStrength));
				Assert.That(request.NormalGrainSize, Is.EqualTo(expectedSize * Scale).Within(1e-8f));
			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		private static void AssertTangentFrames(Mesh mesh)
		{
			var tangents = mesh.tangents;
			var normals = mesh.normals;
			Assert.That(tangents, Has.Length.EqualTo(mesh.vertexCount));
			for (var i = 0; i < tangents.Length; i++) {
				var t = tangents[i];
				Assert.That(math.all(math.isfinite(new float4(t.x, t.y, t.z, t.w))), Is.True, $"tangent {i} is not finite");
				var direction = new Vector3(t.x, t.y, t.z);
				Assert.That(direction.sqrMagnitude, Is.EqualTo(1f).Within(1e-4f), $"tangent {i} is not normalized");
				Assert.That(Vector3.Dot(normals[i], direction), Is.EqualTo(0f).Within(1e-4f), $"tangent {i} is not perpendicular to the normal");
				Assert.That(math.abs(t.w), Is.EqualTo(1f), $"tangent {i} has no handedness");
			}
		}

		[TestCase(InsertShape.Triangle, 36f)]
		[TestCase(InsertShape.Triangle, 60f)]
		[TestCase(InsertShape.Rectangle, 36f)]
		[TestCase(InsertShape.Rectangle, 60f)]
		public void ShouldKeepTheCoverSeparateFromTheBodyBake(InsertShape shape, float width)
		{
			var go = new GameObject("Covered insert");
			var insert = go.AddComponent<InsertComponent>();
			var plain = new Material(Shader.Find("Hidden/InternalErrorShader"));
			var baked = new Material(plain);
			var cover = new Material(plain);
			var probe = new Mesh();
			InsertBakeData bake = null;
			try {
				var settings = new UnityEditor.SerializedObject(insert);
				settings.FindProperty("_shape").enumValueIndex = (int)shape;
				settings.FindProperty("_width").floatValue = width;
				settings.ApplyModifiedPropertiesWithoutUndo();
				var renderer = go.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = plain;
				var layout = new InsertMeshLayout();
				insert.Generate(probe, layout);
				bake = FakeBake(probe, layout.Hash);
				bake.Material = baked;
				insert.SetBake(bake);
				var mesh = go.GetComponent<MeshFilter>().sharedMesh;
				var vertices = mesh.vertices;
				var normals = mesh.normals;
				var uvs = mesh.uv;
				var tangents = mesh.tangents;
				var triangles = mesh.triangles;

				insert.SetCoverMaterial(cover);
				Assert.That(insert.IsBakeFresh, Is.True);
				Assert.That(mesh.subMeshCount, Is.EqualTo(2));
				Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { baked, cover }));
				Assert.That(mesh.GetTriangles(0), Is.EqualTo(triangles));
				Assert.That(mesh.vertices.Take(vertices.Length), Is.EqualTo(vertices));
				Assert.That(mesh.normals.Take(vertices.Length), Is.EqualTo(normals));
				Assert.That(mesh.uv.Take(vertices.Length), Is.EqualTo(uvs));
				Assert.That(mesh.tangents.Take(vertices.Length), Is.EqualTo(tangents));
				var covered = mesh.vertices;
				var cap = mesh.GetTriangles(1);
				Assert.That(cap.Length, Is.GreaterThan(0));
				var area = 0f;
				for (var i = 0; i < cap.Length; i += 3) {
					var a = covered[cap[i]];
					var b = covered[cap[i + 1]];
					var c = covered[cap[i + 2]];
					Assert.That(cap[i], Is.GreaterThanOrEqualTo(vertices.Length));
					Assert.That(a.y, Is.Zero);
					var cross = Vector3.Cross(b - a, c - a);
					Assert.That(cross.y, Is.GreaterThan(0), "Cover triangles face up.");
					area += cross.y * 0.5f;
				}
				var expectedArea = 0f;
				var outline = insert.Outline.Points;
				for (var i = 0; i < outline.Count; i++) {
					expectedArea += Vector3.Cross(InsertMeshGenerator.ToLocal(outline[i].Position),
						InsertMeshGenerator.ToLocal(outline[(i + 1) % outline.Count].Position)).y * 0.5f;
				}
				Assert.That(area, Is.EqualTo(expectedArea).Within(1e-8f), "The cover fills the entire rim.");
				var nextLayout = new InsertMeshLayout();
				insert.Generate(probe, nextLayout);
				Assert.That(nextLayout.Hash, Is.EqualTo(layout.Hash), "Blender still receives only the body.");
				Assert.That(probe.subMeshCount, Is.EqualTo(1));

				settings.Update();
				settings.FindProperty("_width").floatValue = width + 10f;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.IsBakeFresh, Is.False);
				Assert.That(mesh.subMeshCount, Is.EqualTo(2));
				AssertTangentFrames(mesh);
				Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { baked, cover }));
				insert.SetBake(null);
				Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { plain, cover }));
				insert.SetCoverMaterial(null);
				Assert.That(mesh.subMeshCount, Is.EqualTo(1));
				Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { plain }));
			} finally {
				Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(probe);
				Object.DestroyImmediate(bake);
				Object.DestroyImmediate(plain);
				Object.DestroyImmediate(baked);
				Object.DestroyImmediate(cover);
			}
		}

		[TestCase(4f, 3f, 12, 1f, true)]
		[TestCase(0f, 3f, 0, 0f, false)]
		[TestCase(4f, 0f, 0, 1f, false)]
		public void ShouldBuildTangentFramesBeforeBaking(float radius, float channel, int bursts, float bevel, bool holes)
		{
			var (mesh, _) = Generate(radius, channel, bursts, bevel, holes);
			try {
				AssertTangentFrames(mesh);
			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		[TestCase(36.12f)]
		[TestCase(40.05f)]
		[TestCase(60f)]
		public void ShouldRebuildTangentFramesWhenResizingABakedInsert(float width)
		{
			var go = new GameObject("Resized insert");
			var insert = go.AddComponent<InsertComponent>();
			var probe = new Mesh();
			InsertBakeData bake = null;
			try {
				var layout = new InsertMeshLayout();
				insert.Generate(probe, layout);
				bake = FakeBake(probe, layout.Hash);
				insert.SetBake(bake);
				Assert.That(insert.IsBakeFresh, Is.True);
				var mesh = go.GetComponent<MeshFilter>().sharedMesh;

				var serialized = new UnityEditor.SerializedObject(insert);
				serialized.FindProperty("_width").floatValue = width;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.IsBakeFresh, Is.False);
				Assert.That(go.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
				AssertTangentFrames(mesh);
			} finally {
				Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(probe);
				Object.DestroyImmediate(bake);
			}
		}

		[TestCase(3f, 12, 24)]
		[TestCase(0f, 12, 24)]
		[TestCase(3f, 0, 0)]
		[TestCase(0f, 0, 0)]
		public void ShouldDescribeOnlyTheStarburstThatWasGenerated(float channel, int bursts, int expectedSectors)
		{
			var (mesh, layout) = Generate(4f, channel, bursts);
			try {
				Assert.That(layout.OpticalStarburst.z, Is.EqualTo(expectedSectors));
				if (expectedSectors > 0) {
					var center = InsertMeshGenerator.ToLocal(new float2(0f, -40f));
					Assert.That(layout.OpticalStarburst.x, Is.EqualTo(center.x).Within(1e-6f));
					Assert.That(layout.OpticalStarburst.y, Is.EqualTo(center.z).Within(1e-6f));
					Assert.That(layout.OpticalStarburst.w, Is.LessThan(layout.FlatHeight));
				} else {
					Assert.That(layout.OpticalStarburst, Is.EqualTo(Vector4.zero));
				}
			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

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
		public void ShouldCarveTheFloorItselfWithoutAChannel()
		{
			// No channel: the floor is the outline straight down, carrying the
			// starburst, and neither the holes nor the bevel asked for exist, since
			// both need a channel.
			var (mesh, layout) = Generate(4f, 0f, 12, 1f, true);
			try {
				var vertices = mesh.vertices;
				var floor = -Depth * Scale;
				var reach = Offset * Scale + 1e-6f;
				Assert.That(layout.OpticalStarburst.z, Is.GreaterThanOrEqualTo(4f));
				// Every wall vertex is at the rim or on the starburst: nothing rounds
				// off between, and nothing goes down into a hole.
				var walls = Enumerable.Range(0, vertices.Length).Where(i => math.abs(mesh.normals[i].y) < 1e-3f).ToArray();
				Assert.That(walls, Is.Not.Empty);
				foreach (var i in walls) {
					Assert.That(vertices[i].y >= -1e-6f || math.abs(vertices[i].y - floor) <= reach, Is.True, $"wall vertex {i} sits on a bevel or in a hole");
				}
				Assert.That(layout.FlatHeight, Is.EqualTo(floor).Within(1e-7f));
				Assert.That(layout.VertexCharts, Has.Member(layout.FlatChart));
				Assert.That(vertices.All(v => v.y >= floor - reach && v.y <= 1e-6f), "nothing below the starburst or above the rim");
				var surface = Enumerable.Range(0, vertices.Length).Where(i => layout.VertexCharts[i] == layout.FlatChart).ToArray();
				Assert.That(surface, Is.Not.Empty);
				foreach (var i in surface) {
					Assert.That(vertices[i].y, Is.InRange(floor - reach, floor + reach), $"vertex {i}");
				}
				Assert.That(Request(mesh, layout).FlatFaces, Is.Not.Empty);
			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldHoldTheStarburstWithinWhatIsBelowAndAboveIt()
		{
			var holes = new[] { new InsertHole(Vector2.zero, 2f) };
			var body = new InsertBody { Depth = 20f, ChannelWidth = 0f, ChannelDepth = 0f, BottomBevel = 1f, Holes = holes };
			Assert.That(body.StarburstLimit(), Is.EqualTo(20f), "the rim: holes and bevel count for nothing without a channel");
			body.Holes = null;
			Assert.That(body.StarburstLimit(), Is.EqualTo(20f));
			body.ChannelWidth = 3f;
			body.ChannelDepth = 4f;
			Assert.That(body.StarburstLimit(), Is.EqualTo(InsertStarburst.OffsetLimit(20f, 4f)), "with a channel, as before");
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

		[TestCase(4f, 3f, 12, 1f, true)]
		[TestCase(0f, 0f, 0, 0f, false)]
		public void ShouldTellTheWallsFromTheFloors(float radius, float channel, int bursts, float bevel, bool holes)
		{
			// Walls get fewer texels than the floors, so every upright triangle must
			// be in a wall chart, and no triangle lying flat may be. The bottom
			// bevel's foot faces up at its very edge, so it is judged by triangle.
			var (mesh, layout) = Generate(radius, channel, bursts, bevel, holes);
			try {
				var normals = mesh.normals;
				var triangles = mesh.triangles;
				var walls = new HashSet<int>(layout.WallCharts);
				Assert.That(walls, Is.Not.Empty);
				Assert.That(walls, Has.No.Member(layout.FlatChart));
				for (var i = 0; i < triangles.Length; i += 3) {
					var chart = layout.VertexCharts[triangles[i]];
					var up = Enumerable.Range(i, 3).All(k => normals[triangles[k]].y > 0.99f);
					var upright = Enumerable.Range(i, 3).All(k => math.abs(normals[triangles[k]].y) < 0.01f);
					if (up) {
						Assert.That(walls, Has.No.Member(chart), $"triangle {i / 3} lies flat but is laid out as a wall");
					} else if (upright) {
						Assert.That(walls, Has.Member(chart), $"triangle {i / 3} stands upright but is laid out as a floor");
					}
				}

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

		/// <summary>A bake that fits the mesh as generated: its own UVs and tangents, per corner.</summary>
		private static InsertBakeData GeneratorBake(Mesh mesh, Hash128 hash)
		{
			var triangles = mesh.triangles;
			var uvs = mesh.uv;
			var tangents = mesh.tangents;
			var bake = ScriptableObject.CreateInstance<InsertBakeData>();
			bake.Hash = hash.ToString();
			bake.Uvs = triangles.Select(vertex => uvs[vertex]).ToArray();
			bake.Tangents = triangles.Select(vertex => tangents[vertex]).ToArray();
			return bake;
		}

		private static InsertComponent EngravedFlash(GameObject go)
		{
			var insert = go.AddComponent<InsertComponent>();
			var settings = new UnityEditor.SerializedObject(insert);
			settings.FindProperty("_shape").enumValueIndex = (int)InsertShape.Flash;
			settings.FindProperty("_channel").boolValue = false;
			settings.FindProperty("_flashGroove").boolValue = true;
			settings.ApplyModifiedPropertiesWithoutUndo();
			insert.RebuildMesh();
			Assert.That(insert.Groove, Is.Not.Null, string.Join(" | ", insert.Warnings));
			return insert;
		}

		[Test]
		public void ShouldDrawTheBottomOfAnEngravedLineInTheQuadPreview()
		{
			var go = new GameObject("Engraved flash");
			var mesh = new Mesh();
			try {
				var insert = EngravedFlash(go);
				var layout = new InsertMeshLayout();
				insert.Generate(mesh, layout);
				var lines = layout.PreviewEdges;
				var trough = InsertMeshGenerator.ToLocal(float2.zero, -insert.Body.Depth - insert.Groove.Depth).y;
				Assert.That(Enumerable.Range(0, lines.Length / 2).Any(i =>
					math.abs(lines[i * 2].y - trough) < 1e-5f &&
					math.abs(lines[i * 2 + 1].y - trough) < 1e-5f), Is.True,
					"the default V-shaped trough has a visible wire along its bottom");
			} finally {
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(mesh);
			}
		}

		private static bool HasTrough(Mesh mesh, float surface, float trough)
			=> mesh.vertices.Any(v => v.y < surface - 1e-5f && v.y > trough - 1e-5f);

		[Test]
		public void ShouldReplaceTheEngravedMiddleWithAFlatIslandWhileTheBakeFits()
		{
			var go = new GameObject("Engraved flash");
			var probe = new Mesh();
			InsertBakeData bake = null;
			try {
				var insert = EngravedFlash(go);
				var layout = new InsertMeshLayout();
				insert.Generate(probe, layout);
				Assert.That(layout.FlatProjected, Is.True, "a middle with a line keeps its projection through the bake");
				Assert.That(layout.FlatIsland, Is.Not.Null);
				var request = Request(probe, layout);
				Assert.That(request.FlatProjected, Is.True);
				// The bake welds the mesh by position: the middle's faces, trough
				// strips included, have to come out as one piece, or Blender packs
				// them as separate islands and the projection is lost.
				var parent = new Dictionary<int, int>();
				int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
				foreach (var t in request.FlatFaces) {
					for (var c = 0; c < 3; c++) {
						var welded = request.Triangles[t * 3 + c];
						parent.TryAdd(welded, welded);
					}
					for (var c = 1; c < 3; c++) {
						parent[Find(request.Triangles[t * 3 + c])] = Find(request.Triangles[t * 3]);
					}
				}
				Assert.That(parent.Keys.ToList().Select(Find).Distinct().Count(), Is.EqualTo(1), "the middle with its trough welds into one piece");
				var surface = layout.FlatHeight;
				var trough = InsertMeshGenerator.ToLocal(float2.zero, -insert.Body.Depth - insert.Groove.Depth).y;
				var filter = go.GetComponent<MeshFilter>();
				Assert.That(HasTrough(filter.sharedMesh, surface, trough), Is.True, "the trough is in the mesh before a bake");
				Assert.That(insert.IsMiddleFlat, Is.False);

				bake = GeneratorBake(probe, layout.Hash);
				insert.SetBake(bake);
				Assert.That(insert.IsBakeFresh, Is.True);
				Assert.That(insert.IsMiddleFlat, Is.True, "a fitting bake takes the trough out");
				var mesh = filter.sharedMesh;
				Assert.That(HasTrough(mesh, surface, trough), Is.False, "the trough is gone from the mesh");
				Assert.That(mesh.triangles.Length, Is.LessThan(probe.triangles.Length), "the plain island is leaner than the middle with its trough");
				var vertices = mesh.vertices;
				var normals = mesh.normals;
				var uvs = mesh.uv;
				var tangents = mesh.tangents;
				// The island's UVs are the projection the bake laid the middle out
				// with: at the middle's edge, where both meshes have a vertex, they
				// are the generated mesh's own.
				var generated = new Dictionary<Vector2, Vector2>();
				var generatedVertices = probe.vertices;
				var generatedUvs = probe.uv;
				for (var i = 0; i < generatedVertices.Length; i++) {
					if (layout.VertexCharts[i] == layout.FlatChart) {
						generated[new Vector2(generatedVertices[i].x, generatedVertices[i].z)] = generatedUvs[i];
					}
				}
				var island = 0;
				var matched = 0;
				for (var i = 0; i < vertices.Length; i++) {
					if (Mathf.Abs(vertices[i].y - surface) > 1e-5f || normals[i] != Vector3.up) {
						continue;
					}
					island++;
					Assert.That(tangents[i].y, Is.EqualTo(0f).Within(1e-5f), "an island vertex's tangent lies in the island");
					Assert.That(new Vector2(tangents[i].x, tangents[i].z).magnitude, Is.EqualTo(1f).Within(1e-4f));
					Assert.That(Mathf.Abs(tangents[i].w), Is.EqualTo(1f));
					if (generated.TryGetValue(new Vector2(vertices[i].x, vertices[i].z), out var uv)) {
						matched++;
						Assert.That(Vector2.Distance(uvs[i], uv), Is.LessThan(1e-4f), "an island vertex carries the map where the generated middle had it");
					}
				}
				Assert.That(island, Is.GreaterThan(0));
				Assert.That(matched, Is.GreaterThan(island / 2), "most island vertices lie on the middle's edge or its holes");

				// A change to the line makes the bake stale, and the trough comes back.
				var settings = new UnityEditor.SerializedObject(insert);
				settings.FindProperty("_flashGrooveInset").floatValue = 7f;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.IsBakeFresh, Is.False);
				Assert.That(insert.IsMiddleFlat, Is.False);
				Assert.That(HasTrough(filter.sharedMesh, surface, trough), Is.True, "a stale bake shows the trough again");
			} finally {
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(probe);
				if (bake != null) {
					Object.DestroyImmediate(bake);
				}
			}
		}

		[Test]
		public void ShouldKeepTheStarburstInTheMeshUnderAFittingBake()
		{
			var go = new GameObject("Starburst insert");
			var probe = new Mesh();
			InsertBakeData bake = null;
			try {
				var insert = go.AddComponent<InsertComponent>();
				var settings = new UnityEditor.SerializedObject(insert);
				settings.FindProperty("_starburst").boolValue = true;
				settings.FindProperty("_starburstBursts").intValue = 16;
				settings.FindProperty("_channel").boolValue = true;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				var layout = new InsertMeshLayout();
				insert.Generate(probe, layout);
				bake = GeneratorBake(probe, layout.Hash);
				insert.SetBake(bake);
				Assert.That(insert.IsBakeFresh, Is.True);
				Assert.That(insert.IsMiddleFlat, Is.False);
				Assert.That(layout.FlatProjected, Is.False, "a starburst's chart is unwrapped as before");
				var raised = go.GetComponent<MeshFilter>().sharedMesh.vertices.Any(v => v.y > layout.FlatHeight + 1e-4f && v.y < -1e-4f);
				Assert.That(raised, Is.True, "the starburst's ridges stay in the mesh; only an engraved line gives way to a flat island");
			} finally {
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(probe);
				if (bake != null) {
					Object.DestroyImmediate(bake);
				}
			}
		}

		[Test]
		public void ShouldSwitchTheRendererOffWhileTheBakeIsStale()
		{
			var shader = Shader.Find("Visual Pinball/Insert Hybrid");
			if (shader == null) {
				Assert.Ignore("Requires the HDRP insert shader.");
			}
			var go = new GameObject("Engraved flash");
			var material = new Material(shader);
			var probe = new Mesh();
			var again = new Mesh();
			InsertBakeData bake = null;
			InsertBakeData refreshed = null;
			try {
				var insert = EngravedFlash(go);
				var renderer = go.GetComponent<MeshRenderer>();
				var layout = new InsertMeshLayout();
				insert.Generate(probe, layout);
				bake = GeneratorBake(probe, layout.Hash);
				bake.Material = material;
				insert.SetBake(bake);
				var light = go.AddComponent<InsertLightComponent>();
				light.Intensity = 0.6f;
				light.Apply();
				Assert.That(insert.IsShowingGeometry, Is.False);
				Assert.That(renderer.enabled, Is.True, "a fitting bake leaves the renderer on");

				var settings = new UnityEditor.SerializedObject(insert);
				settings.FindProperty("_flashGrooveInset").floatValue = 7f;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.IsShowingGeometry, Is.True);
				Assert.That(renderer.enabled, Is.False, "a stale bake switches the renderer off, the geometry drawn as a gizmo instead");
				var block = new MaterialPropertyBlock();
				renderer.GetPropertyBlock(block, 0);
				Assert.That(block.GetFloat("_InsertLampLevel"), Is.EqualTo(0.6f).Within(1e-5f), "the light's own values are left alone");

				var layoutAgain = new InsertMeshLayout();
				insert.Generate(again, layoutAgain);
				refreshed = GeneratorBake(again, layoutAgain.Hash);
				refreshed.Material = material;
				insert.SetBake(refreshed);
				Assert.That(insert.IsShowingGeometry, Is.False);
				Assert.That(renderer.enabled, Is.True, "a bake that fits again switches the renderer back on");

				// An undo of the change puts the geometry back, and the rebuild
				// that follows puts the renderer back with it: nothing on the
				// component remembers the renderer, so nothing is undone without it.
				UnityEditor.Undo.IncrementCurrentGroup();
				UnityEditor.Undo.RecordObject(insert, "Change the line");
				// A fresh view of the insert: the old one would write its stale
				// copy of the bake back over the refreshed one.
				settings = new UnityEditor.SerializedObject(insert);
				settings.FindProperty("_flashGrooveInset").floatValue = 6f;
				settings.ApplyModifiedProperties();
				insert.RebuildMesh();
				Assert.That(insert.IsShowingGeometry, Is.True);
				Assert.That(renderer.enabled, Is.False);
				UnityEditor.Undo.PerformUndo();
				insert.RebuildMesh();
				Assert.That(insert.IsBakeFresh, Is.True, "the undo put the baked geometry back");
				Assert.That(insert.IsShowingGeometry, Is.False);
				Assert.That(renderer.enabled, Is.True, "and the rebuild after it put the renderer back on");
				// The renderer follows the bake whatever was done to it by hand.
				renderer.enabled = false;
				insert.RebuildMesh();
				Assert.That(renderer.enabled, Is.True, "the insert owns its renderer's on and off");
			} finally {
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(material);
				Object.DestroyImmediate(probe);
				Object.DestroyImmediate(again);
				foreach (var data in new[] { bake, refreshed }) {
					if (data != null) {
						Object.DestroyImmediate(data);
					}
				}
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
		public void ShouldCopyHybridTuningWhenADuplicateGetsItsOwnBakeMaterial()
		{
			var shader = Shader.Find("Visual Pinball/Insert Hybrid");
			if (shader == null) {
				Assert.Ignore("Requires the HDRP insert shader.");
			}
			var root = new GameObject("Original insert");
			var plain = new Material(Shader.Find("Hidden/InternalErrorShader"));
			var hybrid = new Material(shader);
			hybrid.SetFloat("_OpticalVariation", 0.23f);
			hybrid.SetVector("_OpticalLamp", new Vector4(0.003f, -0.02f, 0.004f, 0.007f));
			var probe = new Mesh();
			InsertBakeData bake = null;
			GameObject copy = null;
			var path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/Insert Material Copy Test.mat");
			var texturePath = UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/Insert Material Copy Normal.asset");
			var normalMap = new Texture2D(2, 2);
			try {
				UnityEditor.AssetDatabase.CreateAsset(normalMap, texturePath);
				var insert = root.AddComponent<InsertComponent>();
				root.GetComponent<MeshRenderer>().sharedMaterial = plain;
				var layout = new InsertMeshLayout();
				insert.Generate(probe, layout);
				bake = FakeBake(probe, layout.Hash);
				bake.Material = hybrid;
				insert.SetBake(bake);
				copy = Object.Instantiate(root);
				var duplicate = copy.GetComponent<InsertComponent>();
				duplicate.RebuildMesh();
				Assert.That(duplicate.Bake, Is.SameAs(bake));
				Assert.That(duplicate.UnbakedMaterial, Is.SameAs(plain));

				// A shared bake takes the new-material path, not the in-place update.
				var method = typeof(InsertBaker).GetMethod("BakeMaterial", BindingFlags.Static | BindingFlags.NonPublic);
				var material = (Material)method.Invoke(null, new object[] { duplicate, false, "Assets", "Insert Material Copy Test", normalMap });
				path = UnityEditor.AssetDatabase.GetAssetPath(material);
				Assert.That(path, Does.StartWith("Assets/Insert Material Copy Test"));
				Assert.That(material, Is.Not.SameAs(hybrid));
				Assert.That(material.shader, Is.SameAs(shader));
				Assert.That(material.GetFloat("_OpticalVariation"), Is.EqualTo(0.23f));
				Assert.That(material.GetVector("_OpticalLamp"), Is.EqualTo(hybrid.GetVector("_OpticalLamp")));
				Assert.That(UnityEditor.AssetDatabase.GetAssetPath(material.GetTexture("_NormalMap")), Is.EqualTo(texturePath));
				Assert.That(hybrid.GetTexture("_NormalMap"), Is.Null);
			} finally {
				UnityEditor.AssetDatabase.DeleteAsset(path);
				UnityEditor.AssetDatabase.DeleteAsset(texturePath);
				if (copy != null) {
					Object.DestroyImmediate(copy.GetComponent<MeshFilter>().sharedMesh);
					Object.DestroyImmediate(copy);
				}
				Object.DestroyImmediate(root.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(probe);
				Object.DestroyImmediate(bake);
				Object.DestroyImmediate(plain);
				Object.DestroyImmediate(hybrid);
			}
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
				var tangents = mesh.tangents;
				Assert.That(bake.TryApply(mesh, layout.Hash), Is.False);
				Assert.That(mesh.vertexCount, Is.EqualTo(count));
				Assert.That(mesh.tangents, Is.EqualTo(tangents));

			} finally {
				Object.DestroyImmediate(mesh);
				Object.DestroyImmediate(other);
				Object.DestroyImmediate(bake);
			}
		}

		[Test]
		public void ShouldKeepTheBakedMaterialAsAPreviewUntilTheBakeIsCleared()
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

				// Geometry edits invalidate the bake, but keep the tuned material visible.
				var serialized = new UnityEditor.SerializedObject(insert);
				serialized.FindProperty("_width").floatValue += 1f;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.IsBakeFresh, Is.False);
				Assert.That(renderer.sharedMaterial, Is.SameAs(baked));
				Assert.That(insert.UnbakedMaterial, Is.SameAs(plain));
				AssertTangentFrames(go.GetComponent<MeshFilter>().sharedMesh);

				// Changed back, it fits again.
				serialized.FindProperty("_width").floatValue -= 1f;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.IsBakeFresh, Is.True);
				Assert.That(renderer.sharedMaterial, Is.SameAs(baked));

				// A material chosen by hand is left alone, through rebuilds and
				// through the original material of the bake wandering back on.
				renderer.sharedMaterial = chosen;
				insert.RebuildMesh();
				Assert.That(renderer.sharedMaterial, Is.SameAs(chosen));
				renderer.sharedMaterial = plain;
				insert.RebuildMesh();
				Assert.That(renderer.sharedMaterial, Is.SameAs(plain), "the material the bake was made from is a choice like any other");
				Assert.That(insert.UnbakedMaterial, Is.SameAs(plain));

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
		[Test]
		public void ShouldBakeIntoTheMaterialTheInsertWearsWhenItCarriesTheBakesMaps()
		{
			// A copy of the baked material made for another colour, maps kept,
			// is the bake's own: the next bake goes into it and it stays on.
			// Assets are compared by Unity's own equality: a reimport may hand
			// back another wrapper for the same asset.
			var shader = Shader.Find("HDRP/Lit");
			if (shader == null) {
				Assert.Ignore("Requires HDRP.");
			}
			var folder = UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/Insert Rebake Test");
			UnityEditor.AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
			var root = new GameObject("Insert");
			var probe = new Mesh();
			InsertBakeData bake = null;
			GameObject other = null;
			try {
				var plain = new Material(shader) { name = "Plain" };
				var baked = new Material(shader) { name = "Baked" };
				var recoloured = new Material(shader) { name = "Blue" };
				var oldMap = new Texture2D(2, 2);
				var newMap = new Texture2D(2, 2);
				UnityEditor.AssetDatabase.CreateAsset(oldMap, $"{folder}/Old Map.asset");
				UnityEditor.AssetDatabase.CreateAsset(newMap, $"{folder}/New Map.asset");
				baked.SetTexture("_NormalMap", oldMap);
				recoloured.SetTexture("_NormalMap", oldMap);
				recoloured.SetColor("_BaseColor", Color.blue);
				UnityEditor.AssetDatabase.CreateAsset(plain, $"{folder}/Plain.mat");
				UnityEditor.AssetDatabase.CreateAsset(baked, $"{folder}/Baked.mat");
				UnityEditor.AssetDatabase.CreateAsset(recoloured, $"{folder}/Blue.mat");

				var insert = root.AddComponent<InsertComponent>();
				var renderer = root.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = plain;
				var layout = new InsertMeshLayout();
				insert.Generate(probe, layout);
				bake = FakeBake(probe, layout.Hash);
				bake.NormalMap = oldMap;
				bake.Material = baked;
				UnityEditor.AssetDatabase.CreateAsset(bake, $"{folder}/Bake.asset");
				insert.SetBake(bake);
				bake.Owner = insert.Id;
				Assert.That(renderer.sharedMaterial, Is.EqualTo(baked));

				renderer.sharedMaterial = recoloured;
				Assert.That(InsertBaker.Carries(recoloured, bake), Is.True, "the recoloured copy carries the bake's map, so it is the bake's own");
				var method = typeof(InsertBaker).GetMethod("BakeMaterial", BindingFlags.Static | BindingFlags.NonPublic);
				var material = (Material)method.Invoke(null, new object[] { insert, true, folder, "Insert", newMap });
				Assert.That(material, Is.EqualTo(recoloured));
				Assert.That(recoloured.GetTexture("_NormalMap"), Is.EqualTo(newMap));
				Assert.That(recoloured.GetColor("_BaseColor"), Is.EqualTo(Color.blue));
				Assert.That(baked.GetTexture("_NormalMap"), Is.EqualTo(oldMap), "the older material is left as it was");

				// Wearing the outcome, as the bake does, keeps it on.
				bake.Material = material;
				insert.SetBake(bake);
				Assert.That(renderer.sharedMaterial, Is.EqualTo(recoloured));
				Assert.That(insert.UnbakedMaterial, Is.EqualTo(plain));

				// A material without the bake's maps is not the bake's to write
				// into: a copy of it takes the map, the material stays clean.
				var shared = new Material(shader) { name = "Shared Red" };
				shared.SetColor("_BaseColor", Color.red);
				UnityEditor.AssetDatabase.CreateAsset(shared, $"{folder}/Shared Red.mat");
				renderer.sharedMaterial = shared;
				Assert.That(InsertBaker.Carries(shared, bake), Is.False);
				material = (Material)method.Invoke(null, new object[] { insert, false, folder, "Insert", newMap });
				Assert.That(material, Is.Not.EqualTo(shared));
				Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.red), "the copy keeps the look");
				Assert.That(material.GetTexture("_NormalMap"), Is.EqualTo(newMap));
				Assert.That(shared.GetTexture("_NormalMap"), Is.Null);
				Assert.That(UnityEditor.AssetDatabase.GetAssetPath(material), Is.EqualTo($"{folder}/Insert Shared Red.mat"));

				// The bake's own material, worn by another insert as well, is not
				// written into either: that insert's maps would go with it.
				other = new GameObject("Other insert");
				other.AddComponent<InsertComponent>();
				other.GetComponent<MeshRenderer>().sharedMaterial = recoloured;
				renderer.sharedMaterial = recoloured;
				material = (Material)method.Invoke(null, new object[] { insert, true, folder, "Insert", newMap });
				Assert.That(material, Is.Not.EqualTo(recoloured));
				Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.blue));
				Assert.That(UnityEditor.AssetDatabase.GetAssetPath(material), Is.EqualTo($"{folder}/Insert Blue.mat"));
			} finally {
				if (other != null) {
					Object.DestroyImmediate(other.GetComponent<MeshFilter>().sharedMesh);
					Object.DestroyImmediate(other);
				}
				Object.DestroyImmediate(root.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(probe);
				UnityEditor.AssetDatabase.DeleteAsset(folder);
			}
		}

		[Test]
		public void ShouldTakeTheCoverFromTheRenderersSecondSlot()
		{
			// A cover material put straight onto the renderer, the way any
			// renderer takes one, is the cover; rebuilds do not put the field's
			// material back over it.
			var go = new GameObject("Insert");
			var insert = go.AddComponent<InsertComponent>();
			var shader = Shader.Find("Hidden/InternalErrorShader");
			var body = new Material(shader) { name = "body" };
			var cover = new Material(shader) { name = "cover" };
			var other = new Material(shader) { name = "other" };
			try {
				var renderer = go.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = body;
				insert.SetCoverMaterial(cover);
				Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { body, cover }));

				renderer.sharedMaterials = new[] { body, other };
				insert.RebuildMesh();
				Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { body, other }));
				Assert.That(insert.CoverMaterial, Is.SameAs(other));

				renderer.sharedMaterials = new[] { body, null };
				insert.RebuildMesh();
				Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { body }), "an emptied slot takes the cover off");
				Assert.That(insert.CoverMaterial, Is.Null);
				Assert.That(go.GetComponent<MeshFilter>().sharedMesh.subMeshCount, Is.EqualTo(1));

				insert.SetCoverMaterial(cover);
				renderer.sharedMaterials = new[] { body };
				insert.RebuildMesh();
				Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { body }), "a slot taken away takes the cover off too");
				Assert.That(insert.CoverMaterial, Is.Null);
				Assert.That(go.GetComponent<MeshFilter>().sharedMesh.subMeshCount, Is.EqualTo(1));
			} finally {
				Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(body);
				Object.DestroyImmediate(cover);
				Object.DestroyImmediate(other);
			}
		}

		[Test]
		public void ShouldKeepTheFloorFlatWithTheStarburstOff()
		{
			var root = new GameObject("Insert without starburst");
			var probe = new Mesh();
			try {
				var insert = root.AddComponent<InsertComponent>();
				var serialized = new UnityEditor.SerializedObject(insert);
				serialized.FindProperty("_starburstBursts").intValue = 12;
				serialized.FindProperty("_starburst").boolValue = true;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				var with = new InsertMeshLayout();
				insert.Generate(probe, with);
				Assert.That(with.OpticalStarburst.z, Is.GreaterThanOrEqualTo(4f));

				serialized.FindProperty("_starburst").boolValue = false;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				var without = new InsertMeshLayout();
				insert.Generate(probe, without);
				Assert.That(without.OpticalStarburst.z, Is.EqualTo(0f));

				serialized.FindProperty("_starburst").boolValue = true;
				serialized.FindProperty("_starburstBursts").intValue = 0;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				var none = new InsertMeshLayout();
				insert.Generate(probe, none);
				Assert.That(none.Hash, Is.EqualTo(without.Hash), "off is the same flat floor as no bursts at all");
			} finally {
				Object.DestroyImmediate(root.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(probe);
			}
		}

		[Test]
		public void ShouldGiveADuplicateAnIdOfItsOwn()
		{
			var root = new GameObject("Original insert");
			GameObject copy = null;
			try {
				var insert = root.AddComponent<InsertComponent>();
				Assert.That(insert.Id, Is.Not.Empty);
				var id = insert.Id;
				copy = Object.Instantiate(root);
				var duplicate = copy.GetComponent<InsertComponent>();
				Assert.That(duplicate.Id, Is.Not.Empty);
				Assert.That(duplicate.Id, Is.Not.EqualTo(id));
				Assert.That(insert.Id, Is.EqualTo(id));
				// Disabling and enabling keeps an id; it is not up for grabs.
				root.SetActive(false);
				root.SetActive(true);
				Assert.That(insert.Id, Is.EqualTo(id));
			} finally {
				if (copy != null) {
					Object.DestroyImmediate(copy.GetComponent<MeshFilter>().sharedMesh);
					Object.DestroyImmediate(copy);
				}
				Object.DestroyImmediate(root.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void ShouldOwnOnlyTheBakeMadeForItsId()
		{
			var root = new GameObject("Original insert");
			var probe = new Mesh();
			InsertBakeData bake = null;
			GameObject copy = null;
			try {
				var insert = root.AddComponent<InsertComponent>();
				var layout = new InsertMeshLayout();
				insert.Generate(probe, layout);
				bake = FakeBake(probe, layout.Hash);
				insert.SetBake(bake);

				// Made for this insert: its own, even with a duplicate showing it.
				bake.Owner = insert.Id;
				copy = Object.Instantiate(root);
				var duplicate = copy.GetComponent<InsertComponent>();
				Assert.That(InsertBaker.Owns(insert, bake), Is.True);
				Assert.That(InsertBaker.Owns(duplicate, bake), Is.False);

				// Made for another insert, wherever that one is now: not this one's.
				bake.Owner = "someone else";
				Assert.That(InsertBaker.Owns(insert, bake), Is.False);

				// From before ids: the insert's as long as nobody else shows it.
				bake.Owner = null;
				Assert.That(InsertBaker.Owns(insert, bake), Is.False);
				Object.DestroyImmediate(copy.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(copy);
				copy = null;
				Assert.That(InsertBaker.Owns(insert, bake), Is.True);
				Assert.That(InsertBaker.Owns(insert, null), Is.False);
			} finally {
				if (copy != null) {
					Object.DestroyImmediate(copy.GetComponent<MeshFilter>().sharedMesh);
					Object.DestroyImmediate(copy);
				}
				Object.DestroyImmediate(root.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(probe);
				Object.DestroyImmediate(bake);
			}
		}

		[Test]
		public void ShouldNotLetAnInactiveDuplicatePassForItsOriginal()
		{
			// An inactive object never passes through OnEnable, so the duplicate
			// keeps the original's id until ownership is asked about.
			var root = new GameObject("Original insert");
			var probe = new Mesh();
			InsertBakeData bake = null;
			GameObject copy = null;
			try {
				var insert = root.AddComponent<InsertComponent>();
				var layout = new InsertMeshLayout();
				insert.Generate(probe, layout);
				bake = FakeBake(probe, layout.Hash);
				insert.SetBake(bake);
				bake.Owner = insert.Id;
				var id = insert.Id;

				root.SetActive(false);
				copy = Object.Instantiate(root);
				var duplicate = copy.GetComponent<InsertComponent>();
				Assert.That(duplicate.Id, Is.EqualTo(id), "the duplicate arrives with the original's id");

				Assert.That(InsertBaker.Owns(duplicate, bake), Is.False);
				Assert.That(duplicate.Id, Is.Not.EqualTo(id));
				Assert.That(insert.Id, Is.EqualTo(id));
				Assert.That(InsertBaker.Owns(insert, bake), Is.True);

				// The other way round, the enabled original still keeps its id.
				var second = Object.Instantiate(root).GetComponent<InsertComponent>();
				try {
					Assert.That(second.Id, Is.EqualTo(id));
					Assert.That(InsertBaker.Owns(insert, bake), Is.True);
					Assert.That(insert.Id, Is.EqualTo(id));
					Assert.That(second.Id, Is.Not.EqualTo(id));
				} finally {
					Object.DestroyImmediate(second.GetComponent<MeshFilter>().sharedMesh);
					Object.DestroyImmediate(second.gameObject);
				}
			} finally {
				if (copy != null) {
					Object.DestroyImmediate(copy.GetComponent<MeshFilter>().sharedMesh);
					Object.DestroyImmediate(copy);
				}
				Object.DestroyImmediate(root.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(probe);
				Object.DestroyImmediate(bake);
			}
		}

		[Test]
		public void ShouldAwardNobodyAnIdThatTwoUnenabledInsertsCarry()
		{
			// Both loaded inactive, as from a saved scene: neither passed through
			// OnEnable, so nothing tells the original from the copy.
			var id = System.Guid.NewGuid().ToString("N");
			var original = new GameObject("Unenabled insert");
			var copy = new GameObject("Unenabled copy");
			var probe = new Mesh();
			InsertBakeData bake = null;
			try {
				original.SetActive(false);
				copy.SetActive(false);
				var insert = original.AddComponent<InsertComponent>();
				var duplicate = copy.AddComponent<InsertComponent>();
				foreach (var component in new[] { insert, duplicate }) {
					var serialized = new UnityEditor.SerializedObject(component);
					serialized.FindProperty("_id").stringValue = id;
					serialized.ApplyModifiedPropertiesWithoutUndo();
				}
				var layout = new InsertMeshLayout();
				insert.Generate(probe, layout);
				bake = FakeBake(probe, layout.Hash);
				bake.Owner = id;

				// Whoever asks first is the one renewed; the bake is nobody's to overwrite.
				Assert.That(InsertBaker.Owns(duplicate, bake), Is.False);
				Assert.That(duplicate.Id, Is.Not.EqualTo(id));
				Assert.That(insert.Id, Is.EqualTo(id));
				Assert.That(InsertBaker.Owns(insert, bake), Is.True, "with the copy renewed, the id has one carrier left");

				// Enabling is what settles it the other way round.
				var third = copy.AddComponent<InsertComponent>();
				var again = new UnityEditor.SerializedObject(third);
				again.FindProperty("_id").stringValue = insert.Id;
				again.ApplyModifiedPropertiesWithoutUndo();
				original.SetActive(true);
				Assert.That(InsertBaker.Owns(third, bake), Is.False);
				Assert.That(third.Id, Is.Not.EqualTo(insert.Id));
				Assert.That(InsertBaker.Owns(insert, bake), Is.True);
			} finally {
				foreach (var go in new[] { copy, original }) {
					var mesh = go.GetComponent<MeshFilter>()?.sharedMesh;
					if (mesh != null) {
						Object.DestroyImmediate(mesh);
					}
					Object.DestroyImmediate(go);
				}
				Object.DestroyImmediate(probe);
				Object.DestroyImmediate(bake);
			}
		}

		[Test]
		public void ShouldGiveEveryPrefabInstanceAnIdOfItsOwn()
		{
			var root = new GameObject("Insert Id Prefab Test");
			var path = UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/Insert Id Prefab Test.prefab");
			var probe = new Mesh();
			GameObject prefab = null;
			GameObject first = null;
			GameObject second = null;
			InsertBakeData bake = null;
			try {
				var insert = root.AddComponent<InsertComponent>();
				var layout = new InsertMeshLayout();
				insert.Generate(probe, layout);
				bake = FakeBake(probe, layout.Hash);
				UnityEditor.AssetDatabase.CreateAsset(bake, path.Replace(".prefab", " Bake.asset"));
				insert.SetBake(bake);
				bake.Owner = insert.Id;
				prefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
				var asset = prefab.GetComponent<InsertComponent>();
				Assert.That(asset.Id, Is.EqualTo(insert.Id), "the prefab keeps the id it was saved with");

				first = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
				second = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
				var one = first.GetComponent<InsertComponent>();
				var two = second.GetComponent<InsertComponent>();
				// Instances only show the prefab's id; each gets one of its own, as an override.
				Assert.That(one.Id, Is.Not.EqualTo(asset.Id));
				Assert.That(two.Id, Is.Not.EqualTo(asset.Id));
				Assert.That(one.Id, Is.Not.EqualTo(two.Id));
				// OnEnable puts the override off to the next tick; asking for the id writes it down, unchanged.
				var id = one.Id;
				Assert.That(one.EnsureId(), Is.EqualTo(id));
				Assert.That(new UnityEditor.SerializedObject(one).FindProperty("_id").prefabOverride, Is.True);
				// The prefab's bake is the prefab's, whatever the ids say.
				Assert.That(InsertBaker.Owns(one, bake), Is.False);
				Assert.That(InsertBaker.Owns(two, bake), Is.False);
			} finally {
				foreach (var go in new[] { first, second }) {
					if (go != null) {
						Object.DestroyImmediate(go.GetComponent<MeshFilter>().sharedMesh);
						Object.DestroyImmediate(go);
					}
				}
				Object.DestroyImmediate(root.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(probe);
				UnityEditor.AssetDatabase.DeleteAsset(path);
				UnityEditor.AssetDatabase.DeleteAsset(path.Replace(".prefab", " Bake.asset"));
			}
		}

		[Test]
		public void ShouldTellReadOnlyBakesByTheirPackage()
		{
			// A registry package is immutable; the engine package is referenced by
			// path, which Unity keeps editable; the project's own assets always are.
			Assert.That(InsertBaker.IsReadOnly("Packages/com.unity.test-framework/package.json"), Is.True);
			var engine = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(InsertBaker).Assembly);
			Assert.That(InsertBaker.IsReadOnly($"{engine.assetPath}/package.json"), Is.False);
			Assert.That(InsertBaker.IsReadOnly("Assets/Nothing Here.asset"), Is.False);
			Assert.That(InsertBaker.IsReadOnly((InsertBakeData)null), Is.False);
		}

		[Test]
		public void ShouldForkABakeIntoTheProjectAsAVariant()
		{
			// Asset references are compared by path or by Unity's own equality:
			// a reimport may hand back another wrapper for the same asset.
			var root = new GameObject("Insert Fork Test Insert");
			var probe = new Mesh();
			var folder = UnityEditor.AssetDatabase.GenerateUniqueAssetPath("Assets/Insert Fork Test");
			var hybrid = Shader.Find("Visual Pinball/Insert Hybrid");
			var shader = hybrid != null ? hybrid : Shader.Find("Hidden/InternalErrorShader");
			var plain = new Material(Shader.Find("Hidden/InternalErrorShader"));
			InsertBakeData bake = null;
			try {
				UnityEditor.AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
				var normalMap = new Texture2D(4, 4);
				UnityEditor.AssetDatabase.CreateAsset(normalMap, $"{folder}/Normal.asset");
				var material = new Material(shader);
				if (hybrid != null) {
					material.SetFloat("_OpticalVariation", 0.23f);
				}
				UnityEditor.AssetDatabase.CreateAsset(material, $"{folder}/Source.mat");
				var cover = new Material(Shader.Find("Hidden/InternalErrorShader"));
				UnityEditor.AssetDatabase.CreateAsset(cover, $"{folder}/Cover.mat");

				var insert = root.AddComponent<InsertComponent>();
				root.GetComponent<MeshRenderer>().sharedMaterial = plain;
				var layout = new InsertMeshLayout();
				insert.Generate(probe, layout);
				bake = FakeBake(probe, layout.Hash);
				bake.NormalMap = normalMap;
				bake.Material = material;
				UnityEditor.AssetDatabase.CreateAsset(bake, $"{folder}/Bake.asset");
				insert.SetBake(bake);
				insert.SetCoverMaterial(cover);
				Assert.That(insert.IsBakeFresh, Is.True);
				Assert.That(root.GetComponent<MeshRenderer>().sharedMaterial, Is.EqualTo(material));

				var fork = InsertBaker.Fork(insert);

				Assert.That(fork, Is.Not.EqualTo(bake));
				Assert.That(insert.Bake, Is.EqualTo(fork));
				Assert.That(insert.IsBakeFresh, Is.True);
				// The fork is the insert's own from now on; the source never was.
				Assert.That(fork.Owner, Is.EqualTo(insert.Id));
				Assert.That(InsertBaker.Owns(insert, fork), Is.True);
				var forkPath = UnityEditor.AssetDatabase.GetAssetPath(fork);
				var forkFolder = System.IO.Path.GetDirectoryName(forkPath).Replace('\\', '/');
				Assert.That(forkPath, Does.StartWith("Assets/Inserts/"));
				Assert.That(fork.Hash, Is.EqualTo(bake.Hash));
				Assert.That(fork.Uvs, Is.EqualTo(bake.Uvs));
				// The maps are copies of their own, in the fork's folder.
				var forkMapPath = UnityEditor.AssetDatabase.GetAssetPath(fork.NormalMap);
				Assert.That(forkMapPath, Is.Not.EqualTo($"{folder}/Normal.asset"));
				Assert.That(System.IO.Path.GetDirectoryName(forkMapPath).Replace('\\', '/'), Is.EqualTo(forkFolder));
				Assert.That(fork.NormalMap.width, Is.EqualTo(4));
				// The material is a variant of the source, wearing the copied map and
				// inheriting the source's tuning; the renderer wears it.
				var forkMaterialPath = UnityEditor.AssetDatabase.GetAssetPath(fork.Material);
				Assert.That(System.IO.Path.GetDirectoryName(forkMaterialPath).Replace('\\', '/'), Is.EqualTo(forkFolder));
				Assert.That(UnityEditor.AssetDatabase.GetAssetPath(fork.Material.parent), Is.EqualTo($"{folder}/Source.mat"));
				if (hybrid != null) {
					// Tuning is inherited, not overridden; only the maps are the variant's own.
					Assert.That(fork.Material.GetFloat("_OpticalVariation"), Is.EqualTo(0.23f));
					Assert.That(fork.Material.IsPropertyOverriden(Shader.PropertyToID("_OpticalVariation")), Is.False);
					Assert.That(UnityEditor.AssetDatabase.GetAssetPath(fork.Material.GetTexture("_NormalMap")), Is.EqualTo(forkMapPath));
					Assert.That(fork.Material.IsPropertyOverriden(Shader.PropertyToID("_NormalMap")), Is.True);
				}
				Assert.That(root.GetComponent<MeshRenderer>().sharedMaterial, Is.EqualTo(fork.Material));
				Assert.That(insert.UnbakedMaterial, Is.EqualTo(plain));
				// The source stays as it was.
				var source = UnityEditor.AssetDatabase.LoadAssetAtPath<InsertBakeData>($"{folder}/Bake.asset");
				Assert.That(UnityEditor.AssetDatabase.GetAssetPath(source.NormalMap), Is.EqualTo($"{folder}/Normal.asset"));
				Assert.That(UnityEditor.AssetDatabase.GetAssetPath(source.Material), Is.EqualTo($"{folder}/Source.mat"));
				Assert.That(string.IsNullOrEmpty(source.Owner), Is.True);
				// A cover in the project is left alone; only a read-only one is forked.
				Assert.That(UnityEditor.AssetDatabase.GetAssetPath(insert.CoverMaterial), Is.EqualTo($"{folder}/Cover.mat"));
			} finally {
				// Whatever the fork made, or left behind on failure, is named after the
				// insert; the Inserts folder goes too when the test was all it held.
				if (UnityEditor.AssetDatabase.IsValidFolder("Assets/Inserts")) {
					foreach (var made in UnityEditor.AssetDatabase.GetSubFolders("Assets/Inserts").Where(f => System.IO.Path.GetFileName(f).StartsWith(root.name))) {
						UnityEditor.AssetDatabase.DeleteAsset(made);
					}
					if (UnityEditor.AssetDatabase.FindAssets("", new[] { "Assets/Inserts" }).Length == 0) {
						UnityEditor.AssetDatabase.DeleteAsset("Assets/Inserts");
					}
				}
				UnityEditor.AssetDatabase.DeleteAsset(folder);
				Object.DestroyImmediate(root.GetComponent<MeshFilter>().sharedMesh);
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(probe);
				Object.DestroyImmediate(plain);
			}
		}

	}
}
