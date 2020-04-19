using Unity.Entities;

namespace VisualPinball.Unity.Physics.Collision
{
	public struct ColliderData : IComponentData
	{
		public BlobAssetReference<QuadTreeBlob> Colliders;
	}
}
