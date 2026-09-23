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
		[TestCase(0f, 12, 0)]
		[TestCase(3f, 0, 0)]
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
				var material = (Material)method.Invoke(null, new object[] { duplicate, null, path, normalMap });
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
