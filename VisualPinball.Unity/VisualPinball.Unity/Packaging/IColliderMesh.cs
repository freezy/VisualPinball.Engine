﻿// Visual Pinball Engine
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

// ReSharper disable MemberCanBePrivate.Global

using System.Collections.Generic;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Components implementing this interface have their separate, non-generated collider meshes.
	/// </summary>
	public interface IColliderMesh
	{
		Mesh GetColliderMesh(int index);
		int NumColliderMeshes { get; }
	}

	public struct ColliderMeshMetaPackable
	{
		public string Name;
		public string PrefabGuid;
		public string PathWithinPrefab;
		public bool IsPrefabMeshOverriden;

#if UNITY_EDITOR

		private static bool IsMeshOverridden(IColliderMesh icm, int index)
		{
			var comp = (icm as Component)!;
			// Get the corresponding component from the original prefab asset
			var prefabComponent = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(comp);
			if (prefabComponent == null) {
				return false;
			}
			return icm.GetColliderMesh(index) != (prefabComponent as IColliderMesh)!.GetColliderMesh(index);
		}

		public static ColliderMeshMetaPackable Instantiate(IColliderMesh icm, int index)
		{
			var comp = (icm as Component)!;

			// get the root of the prefab instance
			var rootInstance = UnityEditor.PrefabUtility.GetNearestPrefabInstanceRoot(comp.gameObject);
			if (rootInstance == null) {
				return new ColliderMeshMetaPackable {
					Name = comp.name,
					IsPrefabMeshOverriden = false,
					PrefabGuid = null,
					PathWithinPrefab = null
				};
			}

			// get the prefab asset path
			var assetPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(comp.gameObject);
			if (string.IsNullOrEmpty(assetPath)) {
				// Could not retrieve a path
				return new ColliderMeshMetaPackable {
					Name = comp.name,
					IsPrefabMeshOverriden = false,
					PrefabGuid = null,
					PathWithinPrefab = null
				};
			}

			// convert to GUID
			var guid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
			if (string.IsNullOrEmpty(guid)) {
				return new ColliderMeshMetaPackable {
					Name = comp.name,
					IsPrefabMeshOverriden = false,
					PrefabGuid = null,
					PathWithinPrefab = null
				};
			}

			// get the transform path relative to the prefab root
			var path = UnityEditor.AnimationUtility.CalculateTransformPath(comp.transform, rootInstance.transform);

			return new ColliderMeshMetaPackable {
				Name = comp.name,
				IsPrefabMeshOverriden = IsMeshOverridden(icm, index),
				PrefabGuid = guid,
				PathWithinPrefab = path
			};
		}
#endif
		public static Dictionary<string, ColliderMeshMetaPackable> Unpack(byte[] bytes)
		{
			return PackageApi.Packer.Unpack<Dictionary<string, ColliderMeshMetaPackable>>(bytes);
		}
	}
}
