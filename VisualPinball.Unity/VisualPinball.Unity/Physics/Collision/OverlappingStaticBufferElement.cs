using Unity.Entities;

namespace VisualPinball.Unity.Physics.Collision
{
	[InternalBufferCapacity(0)]
	public struct OverlappingStaticColliderBufferElement : IBufferElementData
	{
		public int Value;
	}
}
