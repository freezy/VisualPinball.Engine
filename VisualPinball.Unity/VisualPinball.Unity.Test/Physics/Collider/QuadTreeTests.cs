using System.Collections.Generic;
using NUnit.Framework;
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
		public void ShouldSerializeCorrectly()
		{
			var v1 = new Vertex2D(1f, 2f);
			var v2 = new Vertex2D(3f, 4f);
			const float zLow = 5f;
			const float zHigh = 6f;
			var lineSeg = new LineSeg(v1, v2, zLow, zHigh);
			var bounds = new Rect3D(true);
			var hitQuad = new HitQuadTree(new List<HitObject> { lineSeg }, bounds);

			var quadTreeBlobAssetRef = QuadTree.CreateBlobAssetReference(hitQuad);
			ref var collider = ref quadTreeBlobAssetRef.Value.HitObjects[0].Value;

			Assert.AreEqual(ColliderType.Line, collider.Type);
		}
	}
}
