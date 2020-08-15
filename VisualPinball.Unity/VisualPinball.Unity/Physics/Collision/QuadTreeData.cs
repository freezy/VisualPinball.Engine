using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct QuadTreeData : IComponentData
	{
		public BlobAssetReference<QuadTreeBlob> Value;
	}
}
