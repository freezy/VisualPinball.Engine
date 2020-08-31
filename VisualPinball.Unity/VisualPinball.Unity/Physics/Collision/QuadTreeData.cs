using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct QuadTreeData : IComponentData
	{
		public BlobAssetReference<QuadTreeBlob> Value;
	}
}
