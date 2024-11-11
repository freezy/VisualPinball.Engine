// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The common data to all colliders.
	///
	/// These are all read-only.
	/// </summary>
	public struct ColliderHeader : IEquatable<ColliderHeader>
	{
		public ColliderType Type;
		public ItemType ItemType;
		public int Id;
		public int ItemId;
		/**
		 * If this is false, that means that the collider's item has a transformation matrix that is not supported
		 * by this collider. It tells the physics runtime to instead transform the ball into this item's space before
		 * testing for collision.
		 */
		public bool IsTransformed;
		public PhysicsMaterialData Material;

		public float Threshold;
		public bool FireEvents;

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
		public readonly bool IsPrimitive
			=> ItemType == ItemType.Primitive
			|| ItemType == ItemType.Ramp
			|| ItemType == ItemType.Rubber
			|| ItemType == ItemType.MetalWireGuide;

		public void Init(ColliderInfo info, ColliderType colliderType)
		{
			if (info.ItemId == 0) {
				throw new InvalidOperationException("Entity of " + info.ItemType + " " + colliderType + " not set!");
			}
			Type = colliderType;
			ItemType = info.ItemType;
			IsTransformed = true; // per default, we assume that we don't have to transform the ball during runtime.
			Id = info.Id;
			ItemId = info.ItemId;
			Material = info.Material;
			Threshold = info.HitThreshold;
			FireEvents = info.FireEvents;
		}

		public static bool operator ==(ColliderHeader a, ColliderHeader b) => a.Equals(b);
		public static bool operator !=(ColliderHeader a, ColliderHeader b) => !a.Equals(b);

		public readonly bool Equals(ColliderHeader other)
			=> Type == other.Type
			&& ItemType == other.ItemType
			&& Id == other.Id
			&& ItemId == other.ItemId
			&& Material == other.Material
			&& Threshold == other.Threshold
			&& FireEvents == other.FireEvents;

		public override readonly bool Equals(object obj)
		{
			if (obj is ColliderHeader)
				return Equals(obj);
			return false;
		}

		public override readonly int GetHashCode() => HashCode.Combine(
			Type, ItemType, Id, ItemId, Material, Threshold, FireEvents);
	}
}
