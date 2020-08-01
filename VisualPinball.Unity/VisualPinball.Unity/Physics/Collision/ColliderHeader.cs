using System;
using Unity.Entities;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT;

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

		public float Threshold;
		public bool FireEvents;
		public bool IsEnabled;

		/// <summary>
		/// Some colliders only collide with "primitives", which aren't only
		/// primitive game items, but can also be ramps and rubbers.
		/// </summary>
		///
		/// <remarks>
		/// That's the reason the `HitObject`'s `m_ObjType` in VPX is set to
		/// `ePrimitive` for those game items.
		///
		/// Only <see cref="Hit3DPoly"/>, <see cref="HitTriangle"/> and
		/// <see cref="HitLine3D"/> check this in order to know whether to emit
		/// the hit event.
		/// </remarks>
		public bool IsPrimitive => ItemType == ItemType.Primitive || ItemType == ItemType.Ramp || ItemType == ItemType.Rubber;

		public void Init(ColliderType type, HitObject src)
		{
			if (src.ItemIndex == 0 && src.ItemVersion == 0) {
				throw new InvalidOperationException("Entity of " + type + " " + src.GetType().Name + " is null!");
			}
			Type = type;
			ItemType = src.ObjType;
			Id = src.Id;
			Entity = new Entity {Index = src.ItemIndex, Version = src.ItemVersion};
			Material = new PhysicsMaterialData {
				Elasticity = src.Elasticity,
				ElasticityFalloff = src.ElasticityFalloff,
				Friction = src.Friction,
				Scatter = src.Scatter,
			};
			Threshold = src.Threshold;
			FireEvents = src.FireEvents;
			IsEnabled = src.IsEnabled;
		}
	}
}
