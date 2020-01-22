using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JeremyAnsel.Media.WavefrontObj;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using Xunit;
using Xunit.Abstractions;

namespace VisualPinball.Engine.Test.Test
{
	public abstract class MeshTests : BaseTests
	{
		protected MeshTests(ITestOutputHelper output) : base(output)
		{
		}

		protected ObjFile LoadObjFixture(string filePath)
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

		protected static void AssertObjMesh(Table table, ObjFile obj, IRenderable renderable, Func<IRenderable, Mesh, string> getName = null, double threshold = FloatThresholdComparer.Threshold)
		{
			var targetMeshes = renderable.GetRenderObjects(table).RenderObjects.Select(ro => ro.Mesh);
			foreach (var mesh in targetMeshes) {
				AssertObjMesh(obj, mesh, getName?.Invoke(renderable, mesh), threshold);
			}
		}

		protected static void AssertObjMesh(ObjFile objFile, string name, Mesh[] meshes, double threshold = FloatThresholdComparer.Threshold)
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

		protected static void AssertObjMesh(ObjFile objFile, Mesh mesh, string name = null, double threshold = FloatThresholdComparer.Threshold)
		{
			name = name ?? mesh.Name;
			var objGroup = objFile.Groups.FirstOrDefault(g => g.Name == name);
			if (objGroup == null) {
				throw new Exception($"Cannot find group {name} in exported obj.");
			}
			var i = 0;
			foreach (var face in objGroup.Faces) {
				AssertVerticesEqual(objFile.Vertices[face.Vertices[2].Vertex - 1].Position, mesh.Vertices[mesh.Indices[i]], threshold);
				AssertVerticesEqual(objFile.Vertices[face.Vertices[1].Vertex - 1].Position, mesh.Vertices[mesh.Indices[i + 1]], threshold);
				AssertVerticesEqual(objFile.Vertices[face.Vertices[0].Vertex - 1].Position, mesh.Vertices[mesh.Indices[i + 2]], threshold);

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

		private static void AssertVerticesEqual(ObjVector4 expected, Vertex3DNoTex2 actual, double threshold = FloatThresholdComparer.Threshold)
		{
			Assert.Equal(expected.X, actual.X, new FloatThresholdComparer(threshold));
			Assert.Equal(expected.Y, actual.Y, new FloatThresholdComparer(threshold));
			Assert.Equal(expected.Z, actual.Z, new FloatThresholdComparer(threshold));
		}

		private class FloatThresholdComparer : IEqualityComparer<float>
		{
			public const double Threshold = 0.0001;
			private readonly double _threshold;

			public FloatThresholdComparer(double threshold)
			{
				_threshold = threshold;
			}

			public bool Equals(float x, float y)
			{
				return System.Math.Abs((double)x - y) <= _threshold;
			}

			public int GetHashCode(float obj)
			{
				return obj.GetHashCode();
			}
		}

	}
}
