﻿using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Physics.Collider;
using VisualPinball.Unity.Physics.Collision;

namespace VisualPinball.Unity.Test.Physics.Collider
{
	[TestFixture]
	[Category("Visual Pinball")]
	public class QuadTreeTests
	{
		[Test]
		public unsafe void ShouldSerializeCorrectly()
		{
			var bounds = new Rect3D(true);
			var hitQuad = new HitQuadTree(new List<HitObject> {
				new LineSeg(new Vertex2D(1f, 2f), new Vertex2D(3f, 4f), 5f, 6f),
				new HitCircle(new Vertex2D(7f, 8f), 9f, 10f, 11f),
				new LineSeg(new Vertex2D(12f, 13f), new Vertex2D(14f, 15f), 16f, 17f),
			}, bounds);

			var quadTreeBlobAssetRef = QuadTreeBlob.CreateBlobAssetReference(
				hitQuad,
				new HitPlane(new Vertex3D(0, 0, 1), 10f),
				new HitPlane(new Vertex3D(0, 0, -1), 20f)
				);
			ref var collider1 = ref quadTreeBlobAssetRef.Value.QuadTree.Colliders[0].Value;
			ref var collider2 = ref quadTreeBlobAssetRef.Value.QuadTree.Colliders[1].Value;
			ref var collider3 = ref quadTreeBlobAssetRef.Value.QuadTree.Colliders[2].Value;
			ref var collider4 = ref quadTreeBlobAssetRef.Value.PlayfieldCollider.Value;

			Assert.AreEqual(ColliderType.Line, collider1.Type);
			Assert.AreEqual(ColliderType.Circle, collider2.Type);
			Assert.AreEqual(ColliderType.Line, collider3.Type);
			Assert.AreEqual(ColliderType.Plane, collider4.Type);
			fixed (Unity.Physics.Collider.Collider* collider = &collider4) {
				Assert.AreEqual(new float3(0f, 0f, 1f), ((PlaneCollider*)collider)->Normal);
			}
		}
	}
}
