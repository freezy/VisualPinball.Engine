using Unity.Entities;

namespace VisualPinball.Unity.Physics.HitTest
{
	public struct CollisionEventData : IComponentData
	{
		public BlobAssetReference<HitObjectData> Obj;
	}
}
