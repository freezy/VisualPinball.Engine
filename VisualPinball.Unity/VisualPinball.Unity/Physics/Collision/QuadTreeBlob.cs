using Unity.Collections;
using Unity.Entities;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Unity
{
	internal struct QuadTreeBlob
	{
		public QuadTree QuadTree;
		public BlobPtr<Collider> PlayfieldCollider;
		public BlobPtr<Collider> GlassCollider;

		public static BlobAssetReference<QuadTreeBlob> CreateBlobAssetReference(Engine.Physics.QuadTree quadTree, HitPlane playfield, HitPlane glass)
		{
			using (var builder = new BlobBuilder(Allocator.Temp)) {
				ref var rootQuadTree = ref builder.ConstructRoot<QuadTreeBlob>();
				QuadTree.Create(quadTree, ref rootQuadTree.QuadTree, builder);

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
