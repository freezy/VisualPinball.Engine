using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Physics.Collider;

namespace VisualPinball.Unity.Test.Physics.Collider
{
	[TestFixture]
	[Category("Visual Pinball")]
	public class QuadTreeTests
	{
		[Test]
		public void ShouldSerializeCorrectly()
		{
			var v1 = new Vertex2D(1f, 2f);
			var v2 = new Vertex2D(3f, 4f);
			const float zLow = 5f;
			const float zHigh = 6f;
			var lineSeg = new LineSeg(v1, v2, zLow, zHigh);
			var bounds = new Rect3D(true);
			var hitQuad = new HitQuadTree(new List<HitObject> { lineSeg }, bounds);

			using (var blobBuilder = new BlobBuilder(Allocator.Temp)) {

				ref var rootQuadTree = ref blobBuilder.ConstructRoot<QuadTree>();

				QuadTree.Create(hitQuad, ref rootQuadTree, blobBuilder);

				var quadTreeBlobAssetRef = blobBuilder.CreateBlobAssetReference<QuadTree>(Allocator.Persistent);
				ref var colliderPtr = ref quadTreeBlobAssetRef.Value.HitObjects[0];
				var collider = colliderPtr.Value;
				var hitTime = collider.HitTest(1);

				Assert.AreEqual(ColliderType.Line, collider.Type);
			}
		}
	}
}
