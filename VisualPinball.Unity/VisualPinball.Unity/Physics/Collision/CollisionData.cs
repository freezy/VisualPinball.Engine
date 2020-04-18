using Unity.Entities;
using VisualPinball.Unity.Physics.Collider;

namespace VisualPinball.Unity.Physics.Collision
{
	public struct CollisionData : IComponentData
	{
		public BlobAssetReference<QuadTree> QuadTree;
		public Collider.Collider PlayfieldCollider;
		public Collider.Collider GlassCollider;

	}
}
