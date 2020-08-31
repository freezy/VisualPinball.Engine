using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct ColliderData : IComponentData
	{
		public BlobAssetReference<ColliderBlob> Value;
	}
}
