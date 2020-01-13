﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using JeremyAnsel.Media.WavefrontObj;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using Xunit;

namespace VisualPinball.Engine.Test.Test
{


	public abstract class MeshTests
	{
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

		protected static void AssertObjMesh(ObjFile objFile, Mesh mesh, string name = null)
		{
			name = name ?? mesh.Name;
			var objGroup = objFile.Groups.FirstOrDefault(g => g.Name == name);
			if (objGroup == null) {
				throw new Exception($"Cannot find group {name} in exported obj.");
			}
			var i = 0;
			foreach (var face in objGroup.Faces) {
				AssertVerticesEqual(objFile.Vertices[face.Vertices[2].Vertex - 1].Position, mesh.Vertices[mesh.Indices[i]]);
				AssertVerticesEqual(objFile.Vertices[face.Vertices[1].Vertex - 1].Position, mesh.Vertices[mesh.Indices[i + 1]]);
				AssertVerticesEqual(objFile.Vertices[face.Vertices[0].Vertex - 1].Position, mesh.Vertices[mesh.Indices[i + 2]]);

				i += 3;
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
			private double _threshold;

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
