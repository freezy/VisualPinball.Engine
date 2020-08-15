using Unity.Entities;

namespace VisualPinball.Unity
{
	[InternalBufferCapacity(0)]
	public struct OverlappingDynamicBufferElement : IBufferElementData
	{
		public Entity Value;
	}
}
