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
using System.IO;
using System.Linq;
using System.Text;

using NUnit.Framework;
using OpenMcdf;

using VisualPinball.Engine.IO.FuturePinball;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Test.IO.FuturePinball
{
	[TestFixture]
	public class FuturePinballGeometryTests
	{
		[Test]
		public void NativeAndDirectScalePathsProduceSameWorldPosition()
		{
			var fp = new Vertex3D(125f, 250f, 30f);
			var vpx = FuturePinballCoordinateConverter.ToVpx(fp.X, fp.Y, fp.Z);
			var direct = FuturePinballCoordinateConverter.ToWorld(fp.X, fp.Y, fp.Z);

			Assert.That(vpx.X / 1852.71f, Is.EqualTo(direct.X).Within(0.000001f));
			Assert.That(vpx.Z / 1852.71f, Is.EqualTo(direct.Y).Within(0.000001f));
			Assert.That(-vpx.Y / 1852.71f, Is.EqualTo(direct.Z).Within(0.000001f));
		}

		[Test]
		public void ConvertsModelAxesScaleNormalsAndTextureV()
		{
			var source = new Mesh(new[] {
				new Vertex3DNoTex2(100f, 200f, 300f, 0f, 1f, 0f, 0.25f, 0.2f),
				new Vertex3DNoTex2(0f, 0f, 0f),
				new Vertex3DNoTex2(1f, 0f, 0f)
			}, new[] { 0, 1, 2 });

			var converted = FuturePinballCoordinateConverter.ModelMeshToWorld(source);

			Assert.That(converted.Vertices[0].X, Is.EqualTo(0.1f));
			Assert.That(converted.Vertices[0].Y, Is.EqualTo(0.2f));
			Assert.That(converted.Vertices[0].Z, Is.EqualTo(-0.3f));
			Assert.That(converted.Vertices[0].Nx, Is.Zero);
			Assert.That(converted.Vertices[0].Ny, Is.EqualTo(1f));
			Assert.That(converted.Vertices[0].Nz, Is.Zero);
			Assert.That(converted.Vertices[0].Tu, Is.EqualTo(0.25f));
			Assert.That(converted.Vertices[0].Tv, Is.EqualTo(0.8f));
			Assert.That(converted.Indices, Is.EqualTo(new[] { 0, 2, 1 }));
			Assert.That(source.Vertices[0].X, Is.EqualTo(100f));
			Assert.That(source.Indices, Is.EqualTo(new[] { 0, 1, 2 }));
		}

		[Test]
		public void BuildsSurfaceFromPointRecords()
		{
			var path = Path.Combine(Path.GetTempPath(), $"vpe-fp-geometry-{Guid.NewGuid():N}.fpt");
			try {
				CreateSurfaceTable(path);
				var table = FuturePinballTableReader.Load(path);
				var points = FuturePinballElementGeometry.Points(table.Elements.Single());
				var generated = FuturePinballProceduralMeshBuilder.Build(table).Single();

				Assert.That(points, Has.Count.EqualTo(4));
				Assert.That(points[1].Position.X, Is.EqualTo(100f));
				Assert.That(points[1].Smooth, Is.True);
				Assert.That(generated.Name, Is.EqualTo("Playfield Wall"));
				Assert.That(generated.IsCollidable, Is.True);
				Assert.That(generated.Texture, Is.EqualTo("wood"));
				Assert.That(generated.Meshes, Has.Count.EqualTo(2));
				Assert.That(generated.Meshes.SelectMany(mesh => mesh.Vertices).Any(vertex =>
					System.Math.Abs(vertex.X - FuturePinballCoordinateConverter.ToVpx(100f)) < 0.001f), Is.True);
				Assert.That(generated.Meshes.SelectMany(mesh => mesh.Vertices).Max(vertex => vertex.Z),
					Is.EqualTo(FuturePinballCoordinateConverter.ToVpx(20f)).Within(0.001f));
			} finally {
				if (File.Exists(path)) File.Delete(path);
			}
		}

		[Test]
		public void BuildsGuideWallFromElementSpecificFloatHeight()
		{
			var path = Path.Combine(Path.GetTempPath(), $"vpe-fp-guide-wall-{Guid.NewGuid():N}.fpt");
			try {
				CreateSurfaceTable(path, FuturePinballElementType.GuideWall);
				var generated = FuturePinballProceduralMeshBuilder.Build(FuturePinballTableReader.Load(path)).Single();

				Assert.That(generated.Type, Is.EqualTo(FuturePinballElementType.GuideWall));
				Assert.That(generated.Meshes.SelectMany(mesh => mesh.Vertices).Max(vertex => vertex.Z),
					Is.EqualTo(FuturePinballCoordinateConverter.ToVpx(20f)).Within(0.001f));
			} finally {
				if (File.Exists(path)) File.Delete(path);
			}
		}

		[TestCaseSource(typeof(FuturePinballFixtureCatalog), nameof(FuturePinballFixtureCatalog.All))]
		public void BuildsFiniteProceduralMeshesFromLockedTable(FuturePinballFixtureExpectation fixture)
		{
			var fixtureRoot = Environment.GetEnvironmentVariable(FuturePinballFixtureCatalog.FixtureRootEnvironmentVariable);
			if (string.IsNullOrWhiteSpace(fixtureRoot)) Assert.Ignore("Future Pinball fixture root is not configured.");
			var path = Path.Combine(fixtureRoot, fixture.RelativePath.Replace('/', Path.DirectorySeparatorChar));
			var generated = FuturePinballProceduralMeshBuilder.Build(FuturePinballTableReader.Load(path));

			Assert.That(generated, Is.Not.Empty);
			Assert.That(generated.SelectMany(element => element.Meshes), Is.Not.Empty);
			foreach (var vertex in generated.SelectMany(element => element.Meshes).SelectMany(mesh => mesh.Vertices)) {
				Assert.That(float.IsNaN(vertex.X) || float.IsInfinity(vertex.X), Is.False);
				Assert.That(float.IsNaN(vertex.Y) || float.IsInfinity(vertex.Y), Is.False);
				Assert.That(float.IsNaN(vertex.Z) || float.IsInfinity(vertex.Z), Is.False);
			}
		}

		private static void CreateSurfaceTable(string path, FuturePinballElementType type = FuturePinballElementType.Surface)
		{
			using (var table = RootStorage.Create(path, OpenMcdf.Version.V3, StorageModeFlags.None)) {
				var storage = table.CreateStorage("Future Pinball");
				Write(storage.CreateStream("File Version"), UInt32(1));
				Write(storage.CreateStream("Table Data"), Join(
					IntegerRecord(0xA5F8BBD1, 500), IntegerRecord(0x9BFCC6D1, 1000),
					IntegerRecord(0x95FDCDD2, 1), IntegerRecord(0xA2F4C9D2, 0),
					IntegerRecord(0xA5F3BFD2, 0), IntegerRecord(0x96ECC5D2, 0),
					IntegerRecord(0xA5F2C5D2, 0), IntegerRecord(0x95F5C9D2, 0),
					IntegerRecord(0x95F5C6D2, 0), IntegerRecord(0x9BFBCED2, 0),
					Record(0xA7FDC4E0, Array.Empty<byte>())
				));
				Write(storage.CreateStream("Table MAC"), new byte[16]);
				Write(storage.CreateStream("Table Element 1"), Join(
					UInt32((uint)type),
					WideStringRecord(0xA4F4D1D7, "Playfield Wall"),
					type == FuturePinballElementType.GuideWall
						? FloatRecord(0xA2F8CDDD, 20f)
						: Join(FloatRecord(0x99F2BEDD, 20f), FloatRecord(0x95F2D0DD, 0f)),
					StringRecord(0xA2F4C9D1, "wood"), IntegerRecord(0x9DF5C3E2, 1),
					Point(0f, 0f, false), Point(100f, 0f, true), Point(100f, 50f, false), Point(0f, 50f, false),
					Record(0xA7FDC4E0, Array.Empty<byte>())
				));
				table.Flush(true);
			}
		}

		private static byte[] Point(float x, float y, bool smooth)
		{
			return Join(
				Record(FuturePinballElementGeometry.PointTag, Array.Empty<byte>()),
				Record(FuturePinballElementGeometry.PositionTag, Join(Single(x), Single(y))),
				IntegerRecord(FuturePinballElementGeometry.SmoothTag, smooth ? 1 : 0),
				Record(0xA7FDC4E0, Array.Empty<byte>())
			);
		}

		private static byte[] IntegerRecord(uint tag, int value) => Record(tag, UInt32((uint)value));
		private static byte[] FloatRecord(uint tag, float value) => Record(tag, Single(value));
		private static byte[] StringRecord(uint tag, string value) => Record(tag, StringBytes(value, Encoding.ASCII));
		private static byte[] WideStringRecord(uint tag, string value) => Record(tag, StringBytes(value, Encoding.Unicode));
		private static byte[] StringBytes(string value, Encoding encoding)
		{
			var bytes = encoding.GetBytes(value);
			return Join(UInt32((uint)bytes.Length), bytes);
		}
		private static byte[] Record(uint tag, byte[] payload) => Join(UInt32((uint)(payload.Length + 4)), UInt32(tag), payload);
		private static byte[] Single(float value) => BitConverter.GetBytes(value);
		private static byte[] UInt32(uint value) => new[] { (byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24) };
		private static byte[] Join(params byte[][] parts)
		{
			var result = new byte[parts.Sum(part => part.Length)];
			var offset = 0;
			foreach (var part in parts) {
				Buffer.BlockCopy(part, 0, result, offset, part.Length);
				offset += part.Length;
			}
			return result;
		}
		private static void Write(CfbStream stream, byte[] data)
		{
			stream.SetLength(data.Length);
			stream.Write(data, 0, data.Length);
			stream.Flush();
		}
	}
}
