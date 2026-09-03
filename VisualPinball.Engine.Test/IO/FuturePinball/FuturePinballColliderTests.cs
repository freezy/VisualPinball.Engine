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

using System.Linq;

using NUnit.Framework;

using VisualPinball.Engine.IO.FuturePinball;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Engine.Test.IO.FuturePinball
{
	[TestFixture]
	public class FuturePinballColliderTests
	{
		[Test]
		public void ConvertsDocumentedAnalyticShapesToModelWorldSpace()
		{
			var colliders = FuturePinballColliderBuilder.FromShapes(new[] {
				new FuturePinballCollisionShape(1, true, 10f, 20f, 30f, 5f, 12f),
				new FuturePinballCollisionShape(2, true, 0f, 0f, 0f, 8f),
				new FuturePinballCollisionShape(3, true, 0f, 4f, 10f, 6f, 3f, 40f, 2f),
				new FuturePinballCollisionShape(5, true, 0f, 0f, 0f, 2f, 3f, 4f),
				new FuturePinballCollisionShape(7, true, 0f, 0f, 0f, 5f, 12f, 6f)
			});

			Assert.That(colliders.Select(collider => collider.Kind), Is.EqualTo(new[] {
				FuturePinballColliderKind.VerticalCylinder,
				FuturePinballColliderKind.Sphere,
				FuturePinballColliderKind.TaperedCapsule,
				FuturePinballColliderKind.Box,
				FuturePinballColliderKind.HorizontalCylinder
			}));
			Assert.That(colliders.All(collider => collider.Status == FuturePinballColliderStatus.Generated), Is.True);
			Assert.That(colliders[0].Center.X, Is.EqualTo(0.01f).Within(0.000001f));
			Assert.That(colliders[0].Center.Y, Is.EqualTo(0.02f).Within(0.000001f));
			Assert.That(colliders[0].Center.Z, Is.EqualTo(-0.03f).Within(0.000001f));
			Assert.That(colliders[0].Radius, Is.EqualTo(0.005f).Within(0.000001f));
			Assert.That(colliders[1].Radius, Is.EqualTo(0.008f).Within(0.000001f));
			Assert.That(colliders[2].Center.Z, Is.EqualTo(0.01f).Within(0.000001f));
			Assert.That(colliders[2].SecondaryRadius, Is.EqualTo(0.003f).Within(0.000001f));
			Assert.That(colliders[3].Size.X, Is.EqualTo(0.004f).Within(0.000001f));
			Assert.That(colliders[3].Size.Y, Is.EqualTo(0.006f).Within(0.000001f));
			Assert.That(colliders[3].Size.Z, Is.EqualTo(0.008f).Within(0.000001f));
			Assert.That(colliders[4].SecondaryRadius, Is.EqualTo(0.006f).Within(0.000001f));
		}

		[Test]
		public void ReportsNonPhysicalUnknownAndInvalidShapes()
		{
			var colliders = FuturePinballColliderBuilder.FromShapes(new[] {
				new FuturePinballCollisionShape(1, false, 0f, 0f, 0f, 1f, 1f),
				new FuturePinballCollisionShape(6, true, 0f, 0f, 0f, 1f),
				new FuturePinballCollisionShape(2, true, 0f, 0f, 0f, float.NaN)
			});

			Assert.That(colliders[0].Status, Is.EqualTo(FuturePinballColliderStatus.Skipped));
			Assert.That(colliders[1].Status, Is.EqualTo(FuturePinballColliderStatus.Unsupported));
			Assert.That(colliders[2].Status, Is.EqualTo(FuturePinballColliderStatus.Invalid));
		}

		[Test]
		public void BuildsPerPolygonMeshAndRejectsDegenerateTriangles()
		{
			var mesh = new Mesh(new[] {
				new Vertex3DNoTex2(0f, 0f, 0f),
				new Vertex3DNoTex2(100f, 0f, 0f),
				new Vertex3DNoTex2(0f, 0f, 100f),
				new Vertex3DNoTex2(5f, 5f, 5f)
			}, new[] { 0, 1, 2, 3, 3, 3 });

			var collider = FuturePinballColliderBuilder.FromMesh(new[] { mesh });

			Assert.That(collider.Status, Is.EqualTo(FuturePinballColliderStatus.Generated));
			Assert.That(collider.Mesh.Indices, Has.Length.EqualTo(3));
			Assert.That(collider.Mesh.Indices, Is.EqualTo(new[] { 0, 2, 1 }));
			Assert.That(collider.Mesh.Vertices[2].Z, Is.EqualTo(-0.1f));
		}

		[Test]
		public void TessellatesGeneratedShapesForVpePrimitiveColliders()
		{
			var descriptions = FuturePinballColliderBuilder.FromShapes(new[] {
				new FuturePinballCollisionShape(1, true, 0f, 0f, 0f, 5f, 12f),
				new FuturePinballCollisionShape(2, true, 0f, 0f, 0f, 8f),
				new FuturePinballCollisionShape(3, true, 0f, 0f, 40f, 6f, 3f, 40f, 2f),
				new FuturePinballCollisionShape(5, true, 0f, 0f, 0f, 2f, 3f, 4f),
				new FuturePinballCollisionShape(7, true, 0f, 0f, 0f, 5f, 12f, 6f)
			});

			var meshes = descriptions.Select(description => FuturePinballColliderMeshBuilder.Build(description)).ToArray();

			Assert.That(meshes.All(mesh => mesh?.IsSet == true && mesh.Indices.Length >= 12), Is.True);
			Assert.That(meshes.All(mesh => mesh.Indices.Length % 3 == 0), Is.True);
			foreach (var mesh in meshes) AssertClosedMeshFacesOutward(mesh);
			AssertBounds(meshes[0], -0.005f, 0.005f, -0.012f, 0.012f, -0.005f, 0.005f);
			AssertBounds(meshes[1], -0.008f, 0.008f, -0.008f, 0.008f, -0.008f, 0.008f);
			AssertBounds(meshes[2], -0.006f, 0.006f, -0.002f, 0.002f, -0.026f, 0.023f);
			AssertBounds(meshes[3], -0.002f, 0.002f, -0.003f, 0.003f, -0.004f, 0.004f);
			AssertBounds(meshes[4], -0.005f, 0.005f, -0.006f, 0.006f, -0.012f, 0.012f);
		}

		[Test]
		public void DoesNotCreateVpeMeshForSkippedOrUnsupportedShapes()
		{
			var descriptions = FuturePinballColliderBuilder.FromShapes(new[] {
				new FuturePinballCollisionShape(1, false, 0f, 0f, 0f, 1f, 1f),
				new FuturePinballCollisionShape(6, true, 0f, 0f, 0f, 1f)
			});

			Assert.That(FuturePinballColliderMeshBuilder.Build(descriptions[0]), Is.Null);
			Assert.That(FuturePinballColliderMeshBuilder.Build(descriptions[1]), Is.Null);
		}

		private static void AssertBounds(Mesh mesh, float minX, float maxX, float minY, float maxY, float minZ, float maxZ)
		{
			Assert.That(mesh.Vertices.Min(vertex => vertex.X), Is.EqualTo(minX).Within(0.000001f));
			Assert.That(mesh.Vertices.Max(vertex => vertex.X), Is.EqualTo(maxX).Within(0.000001f));
			Assert.That(mesh.Vertices.Min(vertex => vertex.Y), Is.EqualTo(minY).Within(0.000001f));
			Assert.That(mesh.Vertices.Max(vertex => vertex.Y), Is.EqualTo(maxY).Within(0.000001f));
			Assert.That(mesh.Vertices.Min(vertex => vertex.Z), Is.EqualTo(minZ).Within(0.000001f));
			Assert.That(mesh.Vertices.Max(vertex => vertex.Z), Is.EqualTo(maxZ).Within(0.000001f));
		}

		private static void AssertClosedMeshFacesOutward(Mesh mesh)
		{
			var centerX = mesh.Vertices.Average(vertex => vertex.X);
			var centerY = mesh.Vertices.Average(vertex => vertex.Y);
			var centerZ = mesh.Vertices.Average(vertex => vertex.Z);
			for (var i = 0; i < mesh.Indices.Length; i += 3) {
				Assert.That(mesh.Indices[i], Is.InRange(0, mesh.Vertices.Length - 1));
				Assert.That(mesh.Indices[i + 1], Is.InRange(0, mesh.Vertices.Length - 1));
				Assert.That(mesh.Indices[i + 2], Is.InRange(0, mesh.Vertices.Length - 1));
				var a = mesh.Vertices[mesh.Indices[i]];
				var b = mesh.Vertices[mesh.Indices[i + 1]];
				var c = mesh.Vertices[mesh.Indices[i + 2]];
				var abX = b.X - a.X;
				var abY = b.Y - a.Y;
				var abZ = b.Z - a.Z;
				var acX = c.X - a.X;
				var acY = c.Y - a.Y;
				var acZ = c.Z - a.Z;
				var normalX = abY * acZ - abZ * acY;
				var normalY = abZ * acX - abX * acZ;
				var normalZ = abX * acY - abY * acX;
				var areaSquared = normalX * normalX + normalY * normalY + normalZ * normalZ;
				Assert.That(areaSquared, Is.GreaterThan(1e-20f));
				var faceX = (a.X + b.X + c.X) / 3f - centerX;
				var faceY = (a.Y + b.Y + c.Y) / 3f - centerY;
				var faceZ = (a.Z + b.Z + c.Z) / 3f - centerZ;
				Assert.That(normalX * faceX + normalY * faceY + normalZ * faceZ, Is.GreaterThan(0f),
					$"{mesh.Name} triangle {i / 3} faces inward");
			}
		}
	}
}
