using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Unity.Physics
{
	public struct StaticKdTree
	{
		public BlobPtr<HitObjectsBlob> HitObjects;
		public BlobPtr<HitQuadTreeBlob> HitQuadTree;

		public static BlobAssetReference<StaticKdTree> Create(IEnumerable<HitObject> hitObjects, HitQuadTree hitQuadTree)
		{
			using (var blobBuilder = new BlobBuilder(Allocator.Temp)) {
				ref var staticKdTree = ref blobBuilder.ConstructRoot<StaticKdTree>();

				HitObjectsBlob.Create(blobBuilder, ref staticKdTree.HitObjects.Value, hitObjects);
				HitQuadTreeBlob.Create(blobBuilder, ref staticKdTree.HitQuadTree.Value, hitQuadTree);

				return blobBuilder.CreateBlobAssetReference<StaticKdTree>(Allocator.Persistent);
			}
		}
	}
}
