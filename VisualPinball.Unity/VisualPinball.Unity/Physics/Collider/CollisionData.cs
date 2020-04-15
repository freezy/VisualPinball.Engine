using Unity.Entities;

namespace VisualPinball.Unity.Physics.Collider
{
	public struct CollisionData : IComponentData
	{
		public BlobAssetReference<QuadTree> QuadTree;
	}
}
