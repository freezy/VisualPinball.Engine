using Unity.Entities;

namespace VisualPinball.Unity.Physics.Collision
{
	[InternalBufferCapacity(0)]
	public struct MatchedBallColliderBufferElement : IBufferElementData
	{
		public Entity Value;
	}
}
