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

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Test.Test;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.MetalWireGuide;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.Math
{
	public class DragPointGoldenTests
	{
		private const string UpdateGoldenEnvironmentVariable = "UPDATE_DRAG_POINT_GOLDENS";

		[Test]
		public void ShouldKeepConsumerGeometryByteIdentical()
		{
			AssertGolden("surface", writer => {
				var container = FileTableContainer.Load(VpxPath.Surface);
				var surface = container.Surface("Wall");
				WriteMesh(writer, surface.GetMesh(SurfaceMeshGenerator.Top, container.Table));
				WriteMesh(writer, surface.GetMesh(SurfaceMeshGenerator.Side, container.Table));
			});

			AssertGolden("ramp", writer => {
				var container = FileTableContainer.Load(VpxPath.Ramp);
				var flat = container.Ramp("Flat");
				WriteMesh(writer, flat.GetMesh(RampMeshGenerator.Floor, container.Table));
				WriteMesh(writer, flat.GetMesh("RightWall", container.Table));
				WriteMesh(writer, flat.GetMesh("LeftWall", container.Table));

				var wire = container.Ramp("Wire4");
				for (var i = 1; i <= 4; i++) {
					WriteMesh(writer, wire.GetMesh($"Wire{i}", container.Table));
				}
			});

			AssertGolden("rubber", writer => {
				var container = FileTableContainer.Load(VpxPath.Rubber);
				var data = container.Rubber("Rubber1").Data;
				WriteMesh(writer, new RubberMeshGenerator(data).GetMesh(data.Height, container.Table));
			});

			AssertGolden("metal-wire-guide", writer => {
				var container = FileTableContainer.Load(VpxPath.MetalWireGuide);
				var data = container.MetalWireGuide("MetalWireGuide2").Data;
				WriteMesh(writer, new MetalWireGuideMeshGenerator(data).GetMesh(container.Table, data));
			});

			AssertGolden("trigger-outline", writer => {
				var container = FileTableContainer.Load(VpxPath.Trigger);
				var dragPoints = container.Trigger("Data").Data.DragPoints;
				AssignStableIds(dragPoints);
				WriteVertices(writer, DragPoint.GetRgVertex<RenderVertex2D,
					CatmullCurve2DCatmullCurveFactory>(dragPoints));
			});

			AssertGolden("light-insert", writer => {
				var container = FileTableContainer.Load(VpxPath.Light);
				var data = container.Light("Light1").Data;
				var generator = new SurfaceMeshGenerator(
					new LightInsertData(data.DragPoints, 20f), Vertex3D.Zero);
				WriteMesh(writer, generator.GetMesh(SurfaceMeshGenerator.Top,
					container.Table.Width, container.Table.Height, 0f, false));
				WriteMesh(writer, generator.GetMesh(SurfaceMeshGenerator.Side,
					container.Table.Width, container.Table.Height, 0f, false));
			});
		}

		[TestCase(false, false, 4f, "open-corners-detail")]
		[TestCase(false, true, 4f, "open-smooth-detail")]
		[TestCase(true, false, 4f, "loop-corners-detail")]
		[TestCase(true, true, 4f, "loop-smooth-detail")]
		[TestCase(false, true, 400f, "open-smooth-coarse")]
		[TestCase(true, true, 400f, "loop-smooth-coarse")]
		public void ShouldKeepTessellationByteIdentical(bool loop, bool smooth, float accuracy,
			string goldenName)
		{
			var dragPoints = CreateTessellationPoints(smooth);
			AssertGolden(goldenName, writer => {
				WriteVertices(writer, DragPoint.GetRgVertex<RenderVertex2D,
					CatmullCurve2DCatmullCurveFactory>(dragPoints, loop, accuracy));
				WriteVertices(writer, DragPoint.GetRgVertex<RenderVertex3D,
					CatmullCurve3DCatmullCurveFactory>(dragPoints, loop, accuracy));
			});
		}

		[Test]
		public void ShouldRoundTripEveryDragPointFieldThroughVpx()
		{
			var cases = new[] {
				(VpxPath.Surface, "Surface", "Wall"),
				(VpxPath.Ramp, "Ramp", "FlatL"),
				(VpxPath.Rubber, "Rubber", "Rubber1"),
				(VpxPath.Trigger, "Trigger", "Data"),
				(VpxPath.MetalWireGuide, "MetalWireGuide", "MetalWireGuide2"),
				(VpxPath.Light, "Light", "Light1"),
			};

			foreach (var (path, itemType, itemName) in cases) {
				var source = FileTableContainer.Load(path);
				var expected = GetDragPoints(source, itemType, itemName);
				var outputPath = Path.Combine(Path.GetTempPath(), $"vpe-dragpoints-{Guid.NewGuid():N}.vpx");
				try {
					source.Export(outputPath);
					var actual = GetDragPoints(FileTableContainer.Load(outputPath), itemType, itemName);
					AssertDragPointsEqual(expected, actual, $"{itemType} {itemName}");
				}
				finally {
					if (File.Exists(outputPath)) {
						File.Delete(outputPath);
					}
				}
			}
		}

		private static DragPointData[] GetDragPoints(FileTableContainer container, string itemType,
			string itemName)
		{
			switch (itemType) {
				case "Surface": return container.Surface(itemName).Data.DragPoints;
				case "Ramp": return container.Ramp(itemName).Data.DragPoints;
				case "Rubber": return container.Rubber(itemName).Data.DragPoints;
				case "Trigger": return container.Trigger(itemName).Data.DragPoints;
				case "MetalWireGuide": return container.MetalWireGuide(itemName).Data.DragPoints;
				case "Light": return container.Light(itemName).Data.DragPoints;
				default: throw new ArgumentOutOfRangeException(nameof(itemType), itemType, null);
			}
		}

		private static void AssertDragPointsEqual(IReadOnlyList<DragPointData> expected,
			IReadOnlyList<DragPointData> actual, string context)
		{
			Assert.That(actual.Count, Is.EqualTo(expected.Count), context);
			for (var i = 0; i < expected.Count; i++) {
				var e = expected[i];
				var a = actual[i];
				Assert.That(a.Center, Is.EqualTo(e.Center), $"{context}, point {i}, center");
				Assert.That(a.IsSmooth, Is.EqualTo(e.IsSmooth), $"{context}, point {i}, smooth");
				Assert.That(a.IsSlingshot, Is.EqualTo(e.IsSlingshot), $"{context}, point {i}, slingshot");
				Assert.That(a.HasAutoTexture, Is.EqualTo(e.HasAutoTexture), $"{context}, point {i}, auto texture");
				Assert.That(a.TextureCoord, Is.EqualTo(e.TextureCoord), $"{context}, point {i}, texture coordinate");
				Assert.That(a.IsLocked, Is.EqualTo(e.IsLocked), $"{context}, point {i}, locked");
				Assert.That(a.EditorLayer, Is.EqualTo(e.EditorLayer), $"{context}, point {i}, editor layer");
				Assert.That(a.EditorLayerName, Is.EqualTo(e.EditorLayerName), $"{context}, point {i}, editor layer name");
				Assert.That(a.EditorLayerVisibility, Is.EqualTo(e.EditorLayerVisibility),
					$"{context}, point {i}, editor layer visibility");
			}
		}

		private static DragPointData[] CreateTessellationPoints(bool smooth)
		{
			return new[] {
				CreatePoint("p0", -120.25f, 15.5f, 1.25f, smooth, true),
				CreatePoint("p1", -25.75f, 140.125f, 18.5f, smooth, false),
				CreatePoint("p2", 95.375f, 80.625f, -7.75f, smooth, true),
				CreatePoint("p3", 155.875f, -45.25f, 32.125f, smooth, false),
				CreatePoint("p4", 5.5f, -110.875f, 4.25f, smooth, false),
			};
		}

		private static DragPointData CreatePoint(string id, float x, float y, float z,
			bool smooth, bool slingshot)
		{
			return new DragPointData(new Vertex3D(x, y, z)) {
				Id = id,
				IsSmooth = smooth,
				IsSlingshot = slingshot,
			};
		}

		private static void AssignStableIds(IReadOnlyList<DragPointData> dragPoints)
		{
			for (var i = 0; i < dragPoints.Count; i++) {
				dragPoints[i].Id = $"p{i}";
			}
		}

		private static void WriteMesh(BinaryWriter writer, Mesh mesh)
		{
			writer.Write(mesh.Name ?? string.Empty);
			writer.Write(mesh.Vertices.Length);
			foreach (var vertex in mesh.Vertices) {
				vertex.Write(writer);
			}

			writer.Write(mesh.Indices.Length);
			foreach (var index in mesh.Indices) {
				writer.Write(index);
			}
		}

		private static void WriteVertices(BinaryWriter writer, IReadOnlyList<RenderVertex2D> vertices)
		{
			writer.Write(vertices.Count);
			foreach (var vertex in vertices) {
				writer.Write(vertex.X);
				writer.Write(vertex.Y);
				WriteVertexMetadata(writer, vertex);
			}
		}

		private static void WriteVertices(BinaryWriter writer, IReadOnlyList<RenderVertex3D> vertices)
		{
			writer.Write(vertices.Count);
			foreach (var vertex in vertices) {
				writer.Write(vertex.X);
				writer.Write(vertex.Y);
				writer.Write(vertex.Z);
				WriteVertexMetadata(writer, vertex);
			}
		}

		private static void WriteVertexMetadata(BinaryWriter writer, IRenderVertex vertex)
		{
			writer.Write(vertex.Smooth);
			writer.Write(vertex.IsSlingshot);
			writer.Write(vertex.IsControlPoint);
			writer.Write(vertex.Id ?? string.Empty);
		}

		private static void AssertGolden(string name, Action<BinaryWriter> write)
		{
			byte[] actual;
			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream)) {
				write(writer);
				writer.Flush();
				actual = stream.ToArray();
			}

			var goldenDirectory = PathHelper.GetFixturePath("DragPointGolden");
			var goldenPath = Path.Combine(goldenDirectory, $"{name}.golden");
			if (Environment.GetEnvironmentVariable(UpdateGoldenEnvironmentVariable) == "1") {
				Directory.CreateDirectory(goldenDirectory);
				File.WriteAllBytes(goldenPath, actual);
			}

			Assert.That(File.Exists(goldenPath), Is.True,
				$"Missing {goldenPath}. Run with {UpdateGoldenEnvironmentVariable}=1 to create it.");
			Assert.That(actual, Is.EqualTo(File.ReadAllBytes(goldenPath)), name);
		}
	}
}
