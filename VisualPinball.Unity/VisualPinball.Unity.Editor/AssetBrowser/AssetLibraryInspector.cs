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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(AssetLibrary))]
	public class AssetLibrarInspector : UnityEditor.Editor
	{
		public override void OnInspectorGUI()
		{
			if (GUILayout.Button("Assign all local assets")) {
				var library = (AssetLibrary)target;
				if (library.IsLocked) {
					Debug.LogError("Cannot assign assets to a locked library. Please unlock the library first.");
					return;
				}
				var assets = AssetDatabase.FindAssets("t:Asset", new[] { library.DatabaseRoot });
				Debug.Log($"Found {assets.Length} assets in library '{library.Name}'.");
				foreach (var assetGuid in assets) {
					var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
					var asset = AssetDatabase.LoadAssetAtPath<Asset>(assetPath);
					if (asset == null) {
						Debug.LogError($"Cannot load asset at {assetPath}.");
						continue;
					}

					var libs = asset.Libraries;

					// everything fine?
					if (libs.Length == 1 && libs[0] == asset.Library) {
						continue;
					}

					// if no lib assigned, assign to current library
					if (libs.Length == 0) {
						Debug.LogWarning($"Asset {asset.Name} is not in any library, assigning to {library.Name}.");
						library.AddAsset(asset);
						continue;
					}

					// multiple libs. remove from other libs and add to current if not already.
					var inCurrentLib = false;
					foreach (var otherLib in libs) {
						if (otherLib == library) {
							inCurrentLib = true;
							asset.Library = library;
							EditorUtility.SetDirty(asset);
							continue;
						}

						if (otherLib.IsLocked) {
							Debug.LogError($"Cannot remove asset {asset.Name} from locked library {otherLib.Name}. Please unlock the library first.");
						} else {
							Debug.Log($"Removing asset {asset.Name} from library {otherLib.Name}.");
							otherLib.RemoveAsset(asset);
						}
					}
					if (!inCurrentLib) {
						Debug.Log($"Adding asset {asset.Name} to library {library.Name}.");
						library.AddAsset(asset);
					}
					AssetDatabase.SaveAssetIfDirty(asset);
				}
			}
			base.OnInspectorGUI();
		}
	}
}
