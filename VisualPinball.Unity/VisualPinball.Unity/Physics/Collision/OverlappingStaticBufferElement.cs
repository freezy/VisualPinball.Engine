using Unity.Entities;

namespace VisualPinball.Unity
{
	[InternalBufferCapacity(0)]
	internal struct OverlappingStaticColliderBufferElement : IBufferElementData
	{
		public int Value;
	}
}
