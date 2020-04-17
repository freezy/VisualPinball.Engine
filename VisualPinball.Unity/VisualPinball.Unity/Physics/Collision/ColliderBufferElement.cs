using Unity.Entities;

namespace VisualPinball.Unity.Physics.Collision
{
	[InternalBufferCapacity(2)]
	public struct ColliderBufferElement : IBufferElementData
	{
		public Collider.Collider Value;
	}
}
