// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

using System.IO;
using System.Web.WebPages;
using UnityEditor;
using UnityEditor.Presets;

namespace VisualPinball.Unity.Editor
{
	public class VpxPostProcessor : AssetPostprocessor
	{
		private static Preset _fbxPreset;

		private void OnPreprocessAsset()
		{
			if (assetPath == null || assetPath.IsEmpty()) {
				return;
			}

			if (Path.GetDirectoryName(assetPath!)!.EndsWith("Meshes") && Path.GetExtension(assetPath).ToLowerInvariant() == ".fbx") {
				if (_fbxPreset == null) {
					const string presetPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Editor/Presets";
					_fbxPreset = AssetDatabase.LoadAssetAtPath<Preset>($"{presetPath}/FBXImporter.preset");
				}
				_fbxPreset.ApplyTo(assetImporter);
			}
		}
	}
}
