using Unity.Entities;

namespace VisualPinball.Unity.Physics.Collision
{
	public struct QuadTreeData : IComponentData
	{
		public BlobAssetReference<QuadTreeBlob> Value;
	}
}
