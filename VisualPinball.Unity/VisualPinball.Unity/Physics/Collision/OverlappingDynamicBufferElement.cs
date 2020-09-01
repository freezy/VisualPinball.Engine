using Unity.Entities;

namespace VisualPinball.Unity
{
	[InternalBufferCapacity(0)]
	internal struct OverlappingDynamicBufferElement : IBufferElementData
	{
		public Entity Value;
	}
}
