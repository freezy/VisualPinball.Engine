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
using MemoryPack;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity
{
	[MemoryPackable]
	public readonly partial struct ItemPackable
	{
		public readonly bool IsActive;
		public readonly bool IsStatic;
		public readonly string PrefabGuid;

		private bool IsEmpty => string.IsNullOrEmpty(PrefabGuid) && IsActive;

		[MemoryPackConstructor]
		public ItemPackable(bool isActive, bool isStatic, string prefabGuid)
		{
			IsActive = isActive;
			IsStatic = isStatic;
			PrefabGuid = prefabGuid;
		}

		public ItemPackable(GameObject go)
		{
			if (PrefabUtility.IsPartOfAnyPrefab(go)) {
				var prefab = PrefabUtility.GetCorrespondingObjectFromSource(go);
				PrefabGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));

			} else {
				PrefabGuid = null;
			}
			IsActive = go.activeInHierarchy;
			IsStatic = go.isStatic;
		}

		public void Apply(GameObject go)
		{
			if (!string.IsNullOrEmpty(PrefabGuid)) {
				var path = AssetDatabase.GUIDToAssetPath(PrefabGuid);
				if (path != null) {
					var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
					if (prefab != null) {
						PrefabUtility.ConvertToPrefabInstance(go, prefab, new ConvertToPrefabInstanceSettings {
							changeRootNameToAssetName = false,
							componentsNotMatchedBecomesOverride = true,
							gameObjectsNotMatchedBecomesOverride = true,
							objectMatchMode = ObjectMatchMode.ByHierarchy,
							recordPropertyOverridesOfMatches = true
						}, InteractionMode.AutomatedAction);
					} else {
						Debug.LogError($"Unable to load prefab {PrefabGuid} at path {path}");
					}
				} else {
					Debug.LogWarning($"Could not find prefab ${PrefabGuid} locally. Asset library missing?");
				}
			}
			go.SetActive(IsActive);
			GameObjectUtility.SetStaticEditorFlags(go, IsStatic ? (StaticEditorFlags)127 : 0);
		}

		public static ItemPackable Unpack(byte[] data) => MemoryPackSerializer.Deserialize<ItemPackable>(data);
		public byte[] Pack() => IsEmpty ? Array.Empty<byte>() : MemoryPackSerializer.Serialize(this);
	}
}
