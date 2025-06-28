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
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// A material variation defines a target (object and material slot of the object) and a list of materials that can
	/// be applied to that target.
	///
	/// Both the variation itself and the overrides can be named.
	/// </summary>
	[Serializable]
	public class AssetMaterialVariation
	{
		public string Name;
		public AssetMaterialTarget Target;
		public List<AssetMaterialOverride> Overrides;

		/// <summary>
		/// If a variation is nested, that means it's part of another asset while looping through material combinations.
		/// </summary>
		[NonSerialized]
		public bool IsNested;

		[NonSerialized]
		public bool IsDecal;
		
		public string GUID => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Target?.Object, out var guid, out long _) ? guid : null;

		public AssetMaterialVariation AsNested()
		{
			IsNested = true;
			return this;
		}

		public AssetMaterialVariation AsDecal()
		{
			IsDecal = true;
			return this;
		}

		public GameObject Match(GameObject go)
		{
			var matchedGo = IsNested ? MatchByGuid(go) : null; // don't match non-nested objects by uuid, since it'll match the root GO.
			if (matchedGo != null) {
				return matchedGo;
			}

			return go.name == Target.Object.name
				? go 
				: go!.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.gameObject.name == Target.Object.name)?.gameObject;
		}

		/// <summary>
		/// We want combinations of nested prefabs to work too, so we can't solely rely on the object name,
		/// since it might be different when nested.
		///
		/// The idea is that we look up the prefab GUID for the object and match it against all prefab
		/// GUIDs of the GameObject's children
		/// </summary>
		/// <param name="go">GameObject in which we look for <see cref="Object"/>.</param>
		/// <returns>Matched GameObject, or null if no match.</returns>
		private GameObject MatchByGuid(GameObject go)
		{
			var objectGuid = GUID;
			if (objectGuid == null) {
				return null;
			}
			foreach (var child in go.GetComponentsInChildren<Transform>(true)) {
				// get reference to prefab
				var prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject);
				if (prefab == null) {
					continue;
				}
				// get GUID of prefab
				if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out var guid, out long _)) {
					if (guid == objectGuid) {
						return child.gameObject;
					}
				}
			}
			return null;
		}

		public override bool Equals(object obj)
		{
			if (obj is AssetMaterialVariation other) {
				return Target == other.Target && Overrides.SequenceEqual(other.Overrides);
			}
			return false;
		}

		protected bool Equals(AssetMaterialVariation other)
		{
			return Name == other.Name && Equals(Overrides, other.Overrides);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Name, Overrides);
		}

		public override string ToString()
			=> $"{Name} - {string.Join(" | ", Overrides.Select(o => o.Name))} ({Target.Object.name}@{Target.Slot})";
	}
}
#endif
