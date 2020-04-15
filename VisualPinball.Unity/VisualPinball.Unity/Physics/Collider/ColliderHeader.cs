namespace VisualPinball.Unity.Physics.Collider
{
	/// <summary>
	/// The common data to all colliders.
	///
	/// These are all read-only.
	/// </summary>
	public struct ColliderHeader
	{
		public ColliderType Type;
		public Aabb HitBBox;
	}
}
