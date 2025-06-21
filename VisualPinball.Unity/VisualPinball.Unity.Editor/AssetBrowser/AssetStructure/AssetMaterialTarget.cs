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

// ReSharper disable InconsistentNaming
#if UNITY_EDITOR
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// A material target is a reference to where a material should be applied to within a prefab.
	/// It consists of the target object and the material slot of that object.
	/// </summary>
	[Serializable]
	public class AssetMaterialTarget : IEquatable<AssetMaterialTarget>
	{
		/// <summary>
		/// Reference to the object that is targeted by this material variation.
		/// </summary>
		[SerializeReference]
		public Object Object;

		/// <summary>
		/// Material slot of the object that is targeted by this material variation.
		/// </summary>
		public int Slot;

		#region IEquatable

		public static bool operator ==(AssetMaterialTarget obj1, AssetMaterialTarget obj2)
		{
			if (ReferenceEquals(obj1, obj2)) {
				return true;
			}

			if (ReferenceEquals(obj1, null)) {
				return false;
			}

			return !ReferenceEquals(obj2, null) && obj1.Equals(obj2);
		}

		public static bool operator !=(AssetMaterialTarget obj1, AssetMaterialTarget obj2) => !(obj1 == obj2);

		public bool Equals(AssetMaterialTarget other)
		{
			if (other == null) {
				return false;
			}
			return Equals(Object, other.Object) && Slot == other.Slot;
		}

		public override bool Equals(object obj)
		{
			if (obj is null) {
				return false;
			}

			if (ReferenceEquals(this, obj)) {
				return true;
			}

			return obj.GetType() == GetType() && Equals((AssetMaterialTarget)obj);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Object, Slot);
		}

		#endregion
	}
}
#endif
