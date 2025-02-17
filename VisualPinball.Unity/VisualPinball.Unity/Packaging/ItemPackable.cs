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

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global

using System;
using UnityEngine;

namespace VisualPinball.Unity
{
	public struct ItemPackable
	{
		public string Name; // useful for debugging, but can be removed at some point.
		public bool IsActive;
		public bool IsStatic;
		public string PrefabGuid;

		private bool IsEmpty => string.IsNullOrEmpty(PrefabGuid) && IsActive && !IsStatic;

#if UNITY_EDITOR
		public static ItemPackable Instantiate(GameObject go)
		{
			return new ItemPackable {
				Name = go.name,
				PrefabGuid = UnityEditor.PrefabUtility.IsPartOfAnyPrefab(go)
					? UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(go)))
					: null,
				IsActive = go.activeInHierarchy,
				IsStatic = go.isStatic
			};
		}

		public void Apply(GameObject go)
		{
			if (!string.IsNullOrEmpty(PrefabGuid) && !UnityEditor.PrefabUtility.IsPartOfAnyPrefab(go)) {
				var path = UnityEditor.AssetDatabase.GUIDToAssetPath(PrefabGuid);
				if (path != null) {
					var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
					if (prefab != null) {
						UnityEditor.PrefabUtility.ConvertToPrefabInstance(go, prefab, new UnityEditor.ConvertToPrefabInstanceSettings {
							changeRootNameToAssetName = false,
							componentsNotMatchedBecomesOverride = true,
							gameObjectsNotMatchedBecomesOverride = true,
							objectMatchMode = UnityEditor.ObjectMatchMode.ByHierarchy,
							recordPropertyOverridesOfMatches = true
						}, UnityEditor.InteractionMode.AutomatedAction);
					} else {
						Debug.LogError($"Unable to load prefab {PrefabGuid} at path {path}");
					}
				} else {
					Debug.LogWarning($"Could not find prefab ${PrefabGuid} locally. Asset library missing?");
				}
			}
			go.SetActive(IsActive);
			UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, IsStatic ? (UnityEditor.StaticEditorFlags)127 : 0);
		}

		public static ItemPackable Unpack(byte[] data) => PackageApi.Packer.Unpack<ItemPackable>(data);
		public byte[] Pack() => IsEmpty ? Array.Empty<byte>() : PackageApi.Packer.Pack(this);
	}
#endif

}
