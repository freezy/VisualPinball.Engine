using Unity.Entities;
using VisualPinball.Unity.Physics.Collider;
using VisualPinball.Unity.VPT;

namespace VisualPinball.Unity.Physics.Collision
{
	/// <summary>
	/// The common data to all colliders.
	///
	/// These are all read-only.
	/// </summary>
	public struct ColliderHeader
	{
		public ColliderType Type;
		public ItemType ItemType;
		public int Id;
		public Entity Entity;
		public PhysicsMaterialData Material;
	}
}
