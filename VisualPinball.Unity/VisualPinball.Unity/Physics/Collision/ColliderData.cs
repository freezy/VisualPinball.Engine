using Unity.Entities;

namespace VisualPinball.Unity
{
	public struct ColliderData : IComponentData
	{
		public BlobAssetReference<ColliderBlob> Value;
	}
}
