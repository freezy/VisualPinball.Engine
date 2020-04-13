using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct QuadTree
	{
		public BlobArray<BlobAssetReference<QuadTree>> Children;
		public float3 Center;
		public BlobArray<BlobPtr<Collider>> HitObjects;
		public bool IsLeaf;

		public static void Create(BlobBuilder blobBuilder, ref QuadTree hitQuadTreeBlob, ref HitQuadTree hitQuadTree)
		{
			// var children = blobBuilder.Allocate(ref hitQuadTreeBlob.Children, 4);
			// for (var i = 0; i < 4; i++) {
			// 	if (hitQuadTree.Children[i] != null) {
			// 		blobBuilder.Allocate(Children[i])
			// 		Create(blobBuilder, ref children[i], hitQuad.Children[i]);
			// 	}
			// }
			hitQuadTreeBlob.Center = hitQuadTree.Center.ToUnityFloat3();
			hitQuadTreeBlob.IsLeaf = hitQuadTree.IsLeaf;


			var colliders = blobBuilder.Allocate(ref hitQuadTreeBlob.HitObjects, hitQuadTree.HitObjects.Count);
			for (var i = 0; i < hitQuadTree.HitObjects.Count; i++) {
				colliders[i] = Collider.CreatePtr(hitQuadTree.HitObjects[i]);
				var child = UnsafeUtilityEx.As<BlobPtr<Collider>, BlobPtr<LineCollider>>(ref colliders[i]);
				blobBuilder.Allocate(ref child);
			}
		}

		// public static QuadTree Create(HitQuadTree hitQuadTree)
		// {
		// 	var children = new BlobArray<QuadTree>();
		// 	for (var i = 0; i < 4; i++) {
		// 		if (hitQuadTree.Children[i] != null) {
		// 			children[i] = Create(hitQuadTree.Children[i]);
		// 		} else {
		// 			children[i] = default;
		// 		}
		// 	}
		//
		// 	return new QuadTree {
		// 		Children = children,
		// 		Center = hitQuadTree.Center.ToUnityFloat3(),
		// 		IsLeaf = hitQuadTree.IsLeaf
		// 	};
		// }
	}
}
