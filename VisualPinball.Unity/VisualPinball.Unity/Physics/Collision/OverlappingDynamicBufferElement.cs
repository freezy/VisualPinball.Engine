using Unity.Entities;

namespace VisualPinball.Unity.Physics.Collision
{
	[InternalBufferCapacity(0)]
	public struct OverlappingDynamicBufferElement : IBufferElementData
	{
		public Entity Value;
	}
}
