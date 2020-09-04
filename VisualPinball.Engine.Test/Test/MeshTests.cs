// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using FluentAssertions;
using JeremyAnsel.Media.WavefrontObj;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Test.Test
{
	public abstract class MeshTests : BaseTests
	{
		private const float Threshold = 0.0001f;

		protected static ObjFile LoadObjFixture(string filePath)
		{
			var lines = File
				.ReadAllLines(filePath)
				.Where(l=> !l.StartsWith("usemtl")) // remove material references
				.Select(l => l.StartsWith("o ") ? "g " + l.Substring(2) : l); // change object to group

			// now, parse
			using (var memStream = new MemoryStream(Encoding.ASCII.GetBytes(string.Join("\n", lines)))) {
				return ObjFile.FromStream(memStream);
			}
		}

		protected static void AssertObjMesh(Table table, ObjFile obj, IRenderable renderable, Func<IRenderable, Mesh, string> getName = null, float threshold = Threshold)
		{
			var targetMeshes = renderable.GetRenderObjects(table).RenderObjects.Select(ro => ro.Mesh);
			foreach (var mesh in targetMeshes) {
				AssertObjMesh(obj, mesh, getName?.Invoke(renderable, mesh), threshold);
			}
		}

		protected static void AssertObjMesh(ObjFile objFile, string name, Mesh[] meshes, float threshold = Threshold)
		{
			var objGroup = objFile.Groups.First(g => g.Name == name);

			// concat all vertices
			var vertices = meshes.SelectMany(m => m.Vertices).ToArray();
			var offset = 0;

			// concat indices, but add additional offset for each new mesh
			var indices = meshes.SelectMany((m, n) => {
				var offset1 = offset;
				var ii = m.Indices.Select(idx => idx + offset1);
				offset += m.Vertices.Length;
				return ii;
			}).ToArray();

			// compare concatenated mesh
			var i = 0;
			foreach (var face in objGroup.Faces) {
				AssertVerticesEqual(objFile.Vertices[face.Vertices[2].Vertex - 1].Position, vertices[indices[i]], threshold);
				AssertVerticesEqual(objFile.Vertices[face.Vertices[1].Vertex - 1].Position, vertices[indices[i + 1]], threshold);
				AssertVerticesEqual(objFile.Vertices[face.Vertices[0].Vertex - 1].Position, vertices[indices[i + 2]], threshold);

				i += 3;
			}
		}

		protected static void AssertObjMesh(ObjFile objFile, Mesh mesh, string name = null, float threshold = Threshold, bool switchZ = false)
		{
			name = name ?? mesh.Name;
			var objGroup = objFile.Groups.FirstOrDefault(g => g.Name == name);
			if (objGroup == null) {
				throw new Exception($"Cannot find group {name} in exported obj.");
			}
			var i = 0;
			foreach (var face in objGroup.Faces) {
				AssertVerticesEqual(objFile.Vertices[face.Vertices[2].Vertex - 1].Position, mesh.Vertices[mesh.Indices[i]], threshold, switchZ);
				AssertVerticesEqual(objFile.Vertices[face.Vertices[1].Vertex - 1].Position, mesh.Vertices[mesh.Indices[i + 1]], threshold, switchZ);
				AssertVerticesEqual(objFile.Vertices[face.Vertices[0].Vertex - 1].Position, mesh.Vertices[mesh.Indices[i + 2]], threshold, switchZ);

				i += 3;
			}
		}

		protected static void AssertNoObjMesh(ObjFile objFile, string name)
		{
			var objGroup = objFile.Groups.FirstOrDefault(g => g.Name == name);
			if (objGroup != null) {
				throw new Exception($"Group {name} must not exist, but does.");
			}
		}

		private static void AssertVerticesEqual(ObjVector4 expected, Vertex3DNoTex2 actual, float threshold, bool switchZ = false)
		{
			var sign = switchZ ? -1 : 1;
			actual.X.Should().BeApproximately(expected.X, threshold);
			actual.Y.Should().BeApproximately(expected.Y, threshold);
			actual.Z.Should().BeApproximately(sign * expected.Z, threshold);
		}
	}
}
