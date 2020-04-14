using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Physics;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct QuadTree
	{
		public BlobArray<BlobPtr<QuadTree>> Children;
		public BlobArray<BlobPtr<Collider>> HitObjects;
		public float3 Center;
		public bool IsLeaf;

		public static void Create(HitQuadTree src, ref QuadTree dest, BlobBuilder builder)
		{
			var children = builder.Allocate(ref dest.Children, 4);
			for (var i = 0; i < 4; i++) {
				if (src.Children[i] != null) {
					Create(src.Children[i], ref children[i], builder);
				}
			}
			dest.Center = src.Center.ToUnityFloat3();
			dest.IsLeaf = src.IsLeaf;

			var colliders = builder.Allocate(ref dest.HitObjects, src.HitObjects.Count);
			for (var i = 0; i < src.HitObjects.Count; i++) {
				Collider.Create(src.HitObjects[i], ref colliders[i], builder);
			}
		}

		private static void Create(HitQuadTree src, ref BlobPtr<QuadTree> dest, BlobBuilder builder)
		{
			Create(src, ref dest.Value, builder);
		}
	}
}
