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
	}
}
