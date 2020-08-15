using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Unity
{
	public struct QuadTreeBlob
	{
		public QuadTree QuadTree;
		public BlobPtr<Collider> PlayfieldCollider;
		public BlobPtr<Collider> GlassCollider;

		public static BlobAssetReference<QuadTreeBlob> CreateBlobAssetReference(HitQuadTree hitQuadTree, HitPlane playfield, HitPlane glass)
		{
			using (var builder = new BlobBuilder(Allocator.Temp)) {
				ref var rootQuadTree = ref builder.ConstructRoot<QuadTreeBlob>();
				QuadTree.Create(hitQuadTree, ref rootQuadTree.QuadTree, builder);

				if (playfield != null) {
					PlaneCollider.Create(builder, playfield, ref rootQuadTree.PlayfieldCollider);

				} else {
					ref var playfieldCollider = ref builder.Allocate(ref rootQuadTree.PlayfieldCollider);
					playfieldCollider.Header = new ColliderHeader {
						Type = ColliderType.None
					};
				}
				PlaneCollider.Create(builder, glass, ref rootQuadTree.GlassCollider);

				return builder.CreateBlobAssetReference<QuadTreeBlob>(Allocator.Persistent);
			}
		}
	}
}
