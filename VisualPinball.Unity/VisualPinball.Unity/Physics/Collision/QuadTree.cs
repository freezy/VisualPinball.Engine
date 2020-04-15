using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.Physics.Collision
{
	public struct QuadTree
	{
		public BlobArray<BlobPtr<QuadTree>> Children;
		public BlobArray<BlobPtr<Collider.Collider>> HitObjects;
		public float3 Center;
		public bool IsLeaf;

		public static BlobAssetReference<QuadTree> CreateBlobAssetReference(HitQuadTree hitQuadTree)
		{
			using (var blobBuilder = new BlobBuilder(Allocator.Temp)) {
				ref var rootQuadTree = ref blobBuilder.ConstructRoot<QuadTree>();
				Create(hitQuadTree, ref rootQuadTree, blobBuilder);
				return blobBuilder.CreateBlobAssetReference<QuadTree>(Allocator.Persistent);
			}
		}

		private static void Create(HitQuadTree src, ref QuadTree dest, BlobBuilder builder)
		{
			var children = builder.Allocate(ref dest.Children, 4);
			for (var i = 0; i < 4; i++) {
				if (src.Children[i] != null) {
					ref var child = ref builder.Allocate(ref children[i]);
					Create(src.Children[i], ref child, builder);
				}
			}

			var colliders = builder.Allocate(ref dest.HitObjects, src.HitObjects.Count);
			for (var i = 0; i < src.HitObjects.Count; i++) {
				Collider.Collider.Create(src.HitObjects[i], ref colliders[i], builder);
			}

			dest.Center = src.Center.ToUnityFloat3();
			dest.IsLeaf = src.IsLeaf;
		}
	}
}
