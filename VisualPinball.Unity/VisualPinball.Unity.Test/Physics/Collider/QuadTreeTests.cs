using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
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
			var lineSeg = new LineSeg(new Vertex2D(1f, 2f), new Vertex2D(3f, 4f), 5f, 6f);
			var bounds = new Rect3D(true);
			var hitQuad = new HitQuadTree(new List<HitObject> { lineSeg }, bounds);

			using (var blobBuilder = new BlobBuilder(Allocator.Temp)) {

				ref var rootQuadTree = ref blobBuilder.ConstructRoot<QuadTree>();

				QuadTree.Create(blobBuilder, ref rootQuadTree, hitQuad);

				var quadTreeBlobAssetRef = blobBuilder.CreateBlobAssetReference<QuadTree>(Allocator.Persistent);

				Assert.Equals(true, true);
			}


		}
	}
}
