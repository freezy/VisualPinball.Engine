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
		/// <summary>
		/// The prefab this game object is an instance of, set on the root of the instance only: the editor import
		/// links the game object to it again.
		/// </summary>
		public string PrefabGuid;
		/// <summary>
		/// The local file id, in the prefab of the instance this game object is part of, of the game object it is the
		/// instance of. The editor import matches it with the prefab's by this id, since names can be overridden.
		/// </summary>
		public long PrefabObject;

		private bool IsEmpty => string.IsNullOrEmpty(PrefabGuid) && PrefabObject == 0 && IsActive && !IsStatic;

		public void ApplyRuntime(GameObject go)
		{
			go.SetActive(IsActive);
			go.isStatic = IsStatic;
		}

		public static ItemPackable Unpack(byte[] data)
		{
			if (data == null || data.Length == 0) {
				return new ItemPackable {
					IsActive = true,
					IsStatic = false,
					PrefabGuid = null
				};
			}

			return PackageApi.Packer.Unpack<ItemPackable>(data);
		}

#if UNITY_EDITOR
		public static ItemPackable Instantiate(GameObject go)
		{
			var item = new ItemPackable {
				Name = go.name,
				IsActive = go.activeInHierarchy,
				IsStatic = go.isStatic
			};
			var root = LinkedRoot(go);
			if (root == null) {
				return item;
			}
			var prefabPath = UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(root));
			if (root == go) {
				item.PrefabGuid = UnityEditor.AssetDatabase.AssetPathToGUID(prefabPath);
			} else {
				var source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSourceAtPath(go, prefabPath);
				if (source && UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out _, out long fileId)) {
					item.PrefabObject = fileId;
				}
			}
			return item;
		}

		/// <summary>
		/// The root of the prefab instance the import links a game object of an instance to: the outermost root, or
		/// the root of an instance added to another. Null for a game object that is not part of an instance, like one
		/// added to it.
		/// </summary>
		public static GameObject LinkedRoot(GameObject go)
		{
			if (!UnityEditor.PrefabUtility.IsPartOfPrefabInstance(go)) {
				return null;
			}
			for (var t = go.transform; t; t = t.parent) {
				var candidate = t.gameObject;
				if (UnityEditor.PrefabUtility.IsAnyPrefabInstanceRoot(candidate)
					&& (UnityEditor.PrefabUtility.IsOutermostPrefabInstanceRoot(candidate) || UnityEditor.PrefabUtility.IsAddedGameObjectOverride(candidate))) {
					return candidate;
				}
			}
			return null;
		}

		public void Apply(GameObject go)
		{
			if (!string.IsNullOrEmpty(PrefabGuid) && !UnityEditor.PrefabUtility.IsPartOfAnyPrefab(go)) {
				var path = UnityEditor.AssetDatabase.GUIDToAssetPath(PrefabGuid);
				if (!string.IsNullOrEmpty(path)) {
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
						Debug.LogWarning($"Unable to load prefab {PrefabGuid} at path {path}.");
					}
				} else {
					Debug.LogWarning($"Could not find prefab {PrefabGuid} locally. Asset library missing?");
				}
			}
			go.SetActive(IsActive);
			UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, IsStatic ? (UnityEditor.StaticEditorFlags)127 : 0);
		}

		public byte[] Pack() => IsEmpty ? Array.Empty<byte>() : PackageApi.Packer.Pack(this);
#endif
	}
}
