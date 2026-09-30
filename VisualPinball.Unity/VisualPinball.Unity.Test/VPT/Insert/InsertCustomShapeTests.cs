// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class InsertCustomShapeTests
	{
		private readonly List<Object> _objects = new();
		private static readonly Vector2[] Profile = {
			new(-50f, -75f), new(50f, -75f), new(50f, 75f), new(0f, 45f), new(-50f, 75f),
		};

		[TearDown]
		public void TearDown()
		{
			foreach (var obj in _objects) { if (obj) { Object.DestroyImmediate(obj); } }
			_objects.Clear();
		}

		private Mesh Wire(Vector2[] points = null, int[] edges = null, MeshTopology topology = MeshTopology.Lines)
		{
			points ??= Profile;
			var mesh = new Mesh { name = "Custom source" };
			_objects.Add(mesh);
			mesh.vertices = points.Select(point => InsertMeshGenerator.ToLocal((float2)point)).ToArray();
			mesh.SetIndices(edges ?? Enumerable.Range(0, points.Length).SelectMany(i => new[] { i, (i + 1) % points.Length }).ToArray(), topology, 0);
			return mesh;
		}

		[Test]
		public void AcceptsClosedConcaveWireWithoutChangingItsSizeOrOrientation()
		{
			var mesh = Wire();
			var outline = InsertOutlineBuilder.Build(InsertCustomShape.Corners(mesh, new InsertCornerSettings(0f, 8)));
			Assert.That(outline.Width, Is.EqualTo(100f).Within(1e-3f));
			Assert.That(outline.Height, Is.EqualTo(150f).Within(1e-3f));
			Assert.That(outline.Corners.Count(corner => corner.Concave), Is.EqualTo(1));
			var loop = InsertCustomShape.Read(mesh);
			foreach (var point in Profile) { Assert.That(loop.Min(p => Vector2.Distance(p, point)), Is.LessThan(1e-3f)); }
		}

		[Test]
		public void FilledMeshAndSplitVertexSeamsProduceTheSameOutlineAsWire()
		{
			var wire = Wire();
			var faces = new[] { 0, 1, 3, 1, 2, 3, 0, 3, 4 };
			var filled = new Mesh();
			_objects.Add(filled);
			filled.vertices = faces.Select(i => wire.vertices[i]).ToArray();
			filled.triangles = Enumerable.Range(0, faces.Length).ToArray();
			Assert.That(InsertCustomShape.Read(filled), Is.EqualTo(InsertCustomShape.Read(wire)));
		}

		[Test]
		public void AcceptsQuadsAndExplicitlyClosedLineStrip()
		{
			var points = new[] { new Vector2(-20, -30), new Vector2(20, -30), new Vector2(20, 30), new Vector2(-20, 30) };
			var quad = Wire(points, new[] { 0, 1, 2, 3 }, MeshTopology.Quads);
			var strip = Wire(points, new[] { 0, 1, 2, 3, 0 }, MeshTopology.LineStrip);
			Assert.That(InsertCustomShape.Read(quad), Is.EqualTo(InsertCustomShape.Read(strip)));
		}

		[Test]
		public void ReversingWindingAndReorderingEdgesDoesNotChangeTheOutline()
		{
			var wire = Wire();
			var reversed = Wire(edges: new[] { 3, 2, 0, 4, 2, 1, 4, 3, 1, 0 });
			Assert.That(InsertCustomShape.Read(reversed), Is.EqualTo(InsertCustomShape.Read(wire)));
		}

		[Test]
		public void AcceptsAnArbitraryFlatPlane()
		{
			var mesh = Wire();
			var original = InsertCustomShape.Read(mesh);
			mesh.vertices = mesh.vertices.Select(p => Quaternion.Euler(32f, 0f, 18f) * p + new Vector3(1, 2, 3)).ToArray();
			var actual = InsertCustomShape.Read(mesh);
			static float Perimeter(Vector2[] points) => Enumerable.Range(0, points.Length).Sum(i => Vector2.Distance(points[i], points[(i + 1) % points.Length]));
			Assert.That(Perimeter(actual), Is.EqualTo(Perimeter(original)).Within(0.01f));
		}

		[Test]
		public void RemovesStraightBoundarySamplesBeforeBuildingCorners()
		{
			var mesh = Wire(new[] { new Vector2(-20, -30), new Vector2(0, -30), new Vector2(20, -30), new Vector2(20, 30), new Vector2(-20, 30) });
			Assert.That(InsertCustomShape.Read(mesh), Has.Length.EqualTo(4));
			Assert.DoesNotThrow(() => InsertOutlineBuilder.Build(InsertCustomShape.Corners(mesh, new InsertCornerSettings(0, 8))));
		}

		[Test]
		public void FilledMeshAcceptsCollinearBoundaryTriangles()
		{
			var points = new[] { new Vector2(-20, -30), new Vector2(0, -30), new Vector2(20, -30), new Vector2(20, 30), new Vector2(-20, 30) };
			var wire = Wire(points);
			var filled = Wire(points, new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4 }, MeshTopology.Triangles);
			Assert.That(InsertCustomShape.Read(filled), Is.EqualTo(InsertCustomShape.Read(wire)));
		}

		[Test]
		public void RejectsNonFlatSource()
		{
			var mesh = Wire();
			var vertices = mesh.vertices;
			vertices[3].y = 0.005f;
			mesh.vertices = vertices;
			Assert.That(Assert.Throws<InsertOutlineException>(() => InsertCustomShape.Read(mesh)).Message, Does.Contain("not flat"));
		}

		[Test]
		public void RejectsOpenSource()
		{
			var mesh = Wire(edges: new[] { 0, 1, 1, 2, 2, 3, 3, 4 });
			Assert.That(Assert.Throws<InsertOutlineException>(() => InsertCustomShape.Read(mesh)).Message, Does.Contain("not closed"));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void RejectsSeparateShapesAndSourceHoles(bool nested)
		{
			var points = new[] { new Vector2(-40, -40), new Vector2(40, -40), new Vector2(40, 40), new Vector2(-40, 40),
				new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10) };
			if (!nested) { for (var i = 4; i < 8; i++) { points[i] += new Vector2(100, 0); } }
			var mesh = Wire(points, new[] { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4 });
			Assert.That(Assert.Throws<InsertOutlineException>(() => InsertCustomShape.Read(mesh)).Message, Does.Contain("more than one"));
		}

		[Test]
		public void RejectsBranchesAndDuplicateEdges()
		{
			var branch = Wire(edges: new[] { 0, 1, 1, 2, 2, 3, 3, 4, 4, 0, 0, 2 });
			Assert.That(Assert.Throws<InsertOutlineException>(() => InsertCustomShape.Read(branch)).Message, Does.Contain("branches"));
			var duplicate = Wire(edges: new[] { 0, 1, 1, 2, 2, 3, 3, 4, 4, 0, 1, 0 });
			Assert.That(Assert.Throws<InsertOutlineException>(() => InsertCustomShape.Read(duplicate)).Message, Does.Contain("duplicate"));
		}

		[Test]
		public void RejectsCrossingsAndTouchingEdges()
		{
			var crossing = Wire(new[] { new Vector2(-20, -20), new Vector2(20, 20), new Vector2(-20, 20), new Vector2(20, -20) });
			Assert.That(Assert.Throws<InsertOutlineException>(() => InsertCustomShape.Read(crossing)).Message, Does.Contain("crosses"));
			var touching = Wire(new[] { new Vector2(-20, -20), new Vector2(20, -20), new Vector2(20, 20), new Vector2(0, -20), new Vector2(-20, 20) });
			Assert.That(Assert.Throws<InsertOutlineException>(() => InsertCustomShape.Read(touching)).Message, Does.Contain("touches"));
		}

		[Test]
		public void RejectsCollinearAndUnsupportedSources()
		{
			var line = Wire(new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(20, 0) });
			Assert.That(Assert.Throws<InsertOutlineException>(() => InsertCustomShape.Read(line)).Message, Does.Contain("collinear"));
			var points = Wire(edges: new[] { 0, 1, 2, 3, 4 }, topology: MeshTopology.Points);
			Assert.That(Assert.Throws<InsertOutlineException>(() => InsertCustomShape.Read(points)).Message, Does.Contain("lines"));
			Assert.That(Assert.Throws<InsertOutlineException>(() => InsertCustomShape.Read(null)).Message, Does.Contain("Assign"));
		}

		[Test]
		public void RejectsUnreadableMeshWithImportSettingGuidance()
		{
			var mesh = Wire();
			mesh.UploadMeshData(true);
			Assert.That(Assert.Throws<InsertOutlineException>(() => InsertCustomShape.Read(mesh)).Message, Does.Contain("Read/Write"));
		}

		[Test]
		public void ReportsInvalidSourceInComponentAndRetainsLastGoodMesh()
		{
			var go = new GameObject("Custom insert");
			_objects.Add(go);
			var insert = go.AddComponent<InsertComponent>();
			Configure(insert, Wire(), 0);
			var generated = go.GetComponent<MeshFilter>().sharedMesh;
			var vertices = generated.vertices;
			insert.CustomMesh = Wire(edges: new[] { 0, 1, 1, 2, 2, 3, 3, 4 });
			Assert.That(insert.Error, Does.Contain("not closed"));
			Assert.That(generated.vertices, Is.EqualTo(vertices));
			var target = new Mesh();
			_objects.Add(target);
			Assert.Throws<InsertOutlineException>(() => insert.Generate(target, new InsertMeshLayout()));
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		public void CustomShapeUsesChannelsHolesBevelAndEveryPattern(int pattern)
		{
			var go = new GameObject("Custom patterned insert");
			_objects.Add(go);
			var insert = go.AddComponent<InsertComponent>();
			Configure(insert, Wire(), pattern);
			Assert.That(insert.Error, Is.Null);
			Assert.That(insert.Outline.Points.Any(p => p.Kind == InsertOutlinePointKind.Arc), Is.True);
			Assert.That(insert.Body.Holes, Has.Length.EqualTo(1));
			var mesh = new Mesh();
			_objects.Add(mesh);
			var layout = new InsertMeshLayout();
			insert.Generate(mesh, layout);
			Assert.That(mesh.triangles.Length, Is.GreaterThan(0));
			Assert.That(layout.Hash, Is.Not.EqualTo(default(Hash128)));
			if (pattern == 2) { Assert.That(insert.Groove, Is.Not.Null); Assert.That(layout.FlatProjected, Is.True); }
			if (pattern == 3) {
				var plain = new Mesh();
				_objects.Add(plain);
				insert.Generate(plain, new InsertMeshLayout(), withoutStipples: true);
				Assert.That(plain.triangles.Length, Is.LessThan(mesh.triangles.Length));
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void EditorSidecarIgnoresUnusedCustomMesh(bool readable)
		{
			var source = new GameObject("Standard insert");
			_objects.Add(source);
			var insert = source.AddComponent<InsertComponent>();
			var mesh = Wire();
			if (!readable) { mesh.UploadMeshData(true); }
			insert.CustomMesh = mesh;
			var captured = InsertAuthoringPayload.Capture(source.transform,
				new Dictionary<Transform, string> { [source.transform] = "standard" }, null, null);
			Assert.That(captured.Inserts[0].CustomMesh, Is.Null);
		}

		[Test]
		public void EditorSidecarRestoresOriginalSourceAndIdenticalBakeGeometry()
		{
			var folder = "Assets/InsertCustomShapeTest-" + Guid.NewGuid().ToString("N");
			var source = new GameObject("Custom source insert");
			var restored = new GameObject("Restored custom insert");
			_objects.Add(source);
			_objects.Add(restored);
			try {
				var insert = source.AddComponent<InsertComponent>();
				Configure(insert, Wire(), 3);
				var before = new Mesh();
				var after = new Mesh();
				_objects.Add(before);
				_objects.Add(after);
				var beforeLayout = new InsertMeshLayout();
				insert.Generate(before, beforeLayout);
				var captured = InsertAuthoringPayload.Capture(source.transform,
					new Dictionary<Transform, string> { [source.transform] = "custom" }, null, null);
				Assert.That(captured.Inserts[0].GeneratorJson, Does.Not.Contain("_customMesh"));
				var payload = PackageApi.Packer.Unpack<InsertAuthoringPayload>(PackageApi.Packer.Pack(captured));
				var imported = restored.AddComponent<InsertComponent>();
				payload.Restore(_ => restored.transform, null, null, folder);
				Assert.That(imported.Error, Is.Null);
				Assert.That(imported.CustomMesh, Is.Not.Null);
				Assert.That(AssetDatabase.Contains(imported.CustomMesh), Is.True);
				Assert.That(imported.CustomMesh.vertices, Is.EqualTo(insert.CustomMesh.vertices));
				var afterLayout = new InsertMeshLayout();
				imported.Generate(after, afterLayout);
				Assert.That(afterLayout.Hash, Is.EqualTo(beforeLayout.Hash));
				Assert.That(after.vertices, Is.EqualTo(before.vertices));
			} finally { AssetDatabase.DeleteAsset(folder); }
		}

		private static void Configure(InsertComponent insert, Mesh mesh, int pattern)
		{
			var properties = new SerializedObject(insert);
			properties.FindProperty("_shape").enumValueIndex = (int)InsertShape.Custom;
			properties.FindProperty("_customMesh").objectReferenceValue = mesh;
			properties.FindProperty("_customCorner").FindPropertyRelative("Radius").floatValue = 1f;
			properties.FindProperty("_starburst").boolValue = pattern == 1;
			properties.FindProperty("_flashGroove").boolValue = pattern == 2;
			properties.FindProperty("_stippled").boolValue = pattern == 3;
			var holes = properties.FindProperty("_holes");
			holes.arraySize = 1;
			holes.GetArrayElementAtIndex(0).FindPropertyRelative("Center").vector2Value = new Vector2(0, -30);
			holes.GetArrayElementAtIndex(0).FindPropertyRelative("Radius").floatValue = 2f;
			properties.ApplyModifiedPropertiesWithoutUndo();
			insert.RebuildMesh();
		}
	}
}
