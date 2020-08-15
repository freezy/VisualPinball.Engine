using Unity.Entities;

namespace VisualPinball.Unity
{
	[InternalBufferCapacity(0)]
	public struct OverlappingStaticColliderBufferElement : IBufferElementData
	{
		public int Value;
	}
}
