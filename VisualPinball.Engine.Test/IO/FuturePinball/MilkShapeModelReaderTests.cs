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

using System.IO;
using System.Text;

using NUnit.Framework;

using VisualPinball.Engine.IO.FuturePinball;

namespace VisualPinball.Engine.Test.IO.FuturePinball
{
	[TestFixture]
	public class MilkShapeModelReaderTests
	{
		[Test]
		public void ParsesModelAndCreatesGroupMesh()
		{
			var model = MilkShapeModelReader.Parse(CreateModel(), "fixture.ms3d");

			Assert.That(model.Version, Is.EqualTo(4));
			Assert.That(model.Vertices, Has.Count.EqualTo(3));
			Assert.That(model.Triangles, Has.Count.EqualTo(1));
			Assert.That(model.Groups, Has.Count.EqualTo(1));
			Assert.That(model.Materials, Has.Count.EqualTo(1));
			Assert.That(model.Materials[0].Texture, Is.EqualTo("texture.png"));
			Assert.That(model.TrailingData, Is.EqualTo(new byte[] { 0xaa, 0xbb, 0xcc }));

			var meshes = model.CreateMeshes();
			Assert.That(meshes, Has.Count.EqualTo(1));
			Assert.That(meshes[0].Name, Is.EqualTo("triangle"));
			Assert.That(meshes[0].MaterialIndex, Is.Zero);
			Assert.That(meshes[0].Mesh.Vertices, Has.Length.EqualTo(3));
			Assert.That(meshes[0].Mesh.Indices, Is.EqualTo(new[] { 0, 1, 2 }));
			Assert.That(meshes[0].Mesh.Vertices[1].X, Is.EqualTo(1f));
			Assert.That(meshes[0].Mesh.Vertices[2].Y, Is.EqualTo(1f));
			Assert.That(meshes[0].Mesh.Vertices[0].Nz, Is.EqualTo(1f));
			Assert.That(meshes[0].Mesh.Vertices[1].Tu, Is.EqualTo(1f));
			Assert.That(meshes[0].Mesh.Vertices[2].Tv, Is.EqualTo(1f));
		}

		[Test]
		public void ReusesModelsByContentHash()
		{
			var data = CreateModel();
			var cache = new MilkShapeModelCache();

			var first = cache.Parse(data, "first.ms3d");
			var second = cache.Parse((byte[])data.Clone(), "second.ms3d");

			Assert.That(second, Is.SameAs(first));
		}

		[Test]
		public void RejectsTriangleVertexOutsideModel()
		{
			var action = new TestDelegate(() => MilkShapeModelReader.Parse(CreateModel(3), "bad.ms3d"));

			Assert.That(action, Throws.TypeOf<FuturePinballFormatException>()
				.With.Message.Contains("outside 0..2")
				.And.Message.Contains("bad.ms3d"));
		}

		private static byte[] CreateModel(ushort firstTriangleVertex = 0)
		{
			using (var stream = new MemoryStream())
			using (var writer = new BinaryWriter(stream, Encoding.ASCII)) {
				writer.Write(Encoding.ASCII.GetBytes("MS3D000000"));
				writer.Write(4);
				writer.Write((ushort)3);
				WriteVertex(writer, 0f, 0f, 0f);
				WriteVertex(writer, 1f, 0f, 0f);
				WriteVertex(writer, 0f, 1f, 0f);

				writer.Write((ushort)1);
				writer.Write((ushort)0);
				writer.Write(firstTriangleVertex);
				writer.Write((ushort)1);
				writer.Write((ushort)2);
				for (var corner = 0; corner < 3; corner++) {
					writer.Write(0f);
					writer.Write(0f);
					writer.Write(1f);
				}
				writer.Write(0f);
				writer.Write(1f);
				writer.Write(0f);
				writer.Write(0f);
				writer.Write(0f);
				writer.Write(1f);
				writer.Write((byte)1);
				writer.Write((byte)0);

				writer.Write((ushort)1);
				writer.Write((byte)0);
				WriteFixedString(writer, "triangle", 32);
				writer.Write((ushort)1);
				writer.Write((ushort)0);
				writer.Write((sbyte)0);

				writer.Write((ushort)1);
				WriteFixedString(writer, "material", 32);
				WriteVector4(writer, 0.2f, 0.2f, 0.2f, 1f);
				WriteVector4(writer, 0.8f, 0.7f, 0.6f, 1f);
				WriteVector4(writer, 1f, 1f, 1f, 1f);
				WriteVector4(writer, 0f, 0f, 0f, 1f);
				writer.Write(16f);
				writer.Write(0.75f);
				writer.Write((byte)0);
				WriteFixedString(writer, "texture.png", 128);
				WriteFixedString(writer, "alpha.png", 128);
				writer.Write(new byte[] { 0xaa, 0xbb, 0xcc });
				return stream.ToArray();
			}
		}

		private static void WriteVertex(BinaryWriter writer, float x, float y, float z)
		{
			writer.Write((byte)0);
			writer.Write(x);
			writer.Write(y);
			writer.Write(z);
			writer.Write((sbyte)-1);
			writer.Write((byte)0);
		}

		private static void WriteVector4(BinaryWriter writer, float x, float y, float z, float w)
		{
			writer.Write(x);
			writer.Write(y);
			writer.Write(z);
			writer.Write(w);
		}

		private static void WriteFixedString(BinaryWriter writer, string value, int length)
		{
			var bytes = Encoding.ASCII.GetBytes(value);
			writer.Write(bytes);
			writer.Write(new byte[length - bytes.Length]);
		}
	}
}
