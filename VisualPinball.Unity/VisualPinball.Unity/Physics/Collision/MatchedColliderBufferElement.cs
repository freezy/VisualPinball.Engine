using Unity.Entities;

namespace VisualPinball.Unity.Physics.Collision
{
	[InternalBufferCapacity(0)]
	public struct MatchedColliderBufferElement : IBufferElementData
	{
		public int Value;
	}
}
