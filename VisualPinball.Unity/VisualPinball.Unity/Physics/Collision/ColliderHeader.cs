using Unity.Entities;
using VisualPinball.Engine.Physics;
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

		public void Init(ColliderType type, HitObject src)
		{
			Type = type;
			ItemType = Collider.Collider.GetItemType(src.ObjType);
			Id = src.Id;
			Entity = new Entity {Index = src.ItemIndex, Version = src.ItemVersion};
			Material = new PhysicsMaterialData {
				Elasticity = src.Elasticity,
				ElasticityFalloff = src.ElasticityFalloff,
				Friction = src.Friction,
				Scatter = src.Scatter,
			};
		}
	}
}
