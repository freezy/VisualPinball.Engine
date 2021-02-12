// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using Unity.Entities;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
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
		public Entity ParentEntity;
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

		public void Init(ColliderInfo info, ColliderType colliderType)
		{
			if (info.Entity == Entity.Null) {
				throw new InvalidOperationException("Entity of " + info.ItemType + " " + colliderType + " not set!");
			}
			Type = colliderType;
			ItemType = info.ItemType;
			Id = info.Id;
			Entity = info.Entity;
			ParentEntity = info.ParentEntity != Entity.Null
				? info.ParentEntity
				: Entity;
			Material = info.Material;
			Threshold = info.HitThreshold;
			FireEvents = info.FireEvents;
			IsEnabled = info.IsEnabled;
		}
	}
}
