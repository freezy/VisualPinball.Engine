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

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Plastic.Antlr3.Runtime.Debug;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;


namespace VisualPinball.Unity.Editor
{
	public partial class AssetBrowserX : EditorWindow
	{
		[SerializeField]
		private int selectedIndex = -1;

		private ToolbarButton _refreshButton;
		private VisualElement _rightPane;

		private List<AssetLibrary> _assetLibraries;

		[MenuItem("Visual Pinball/Asset Browser X")]
		public static void Init()
		{
			var wnd = GetWindow<AssetBrowserX>("Asset Browser X");

			// Limit size of the window
			wnd.minSize = new Vector2(450, 200);
			wnd.maxSize = new Vector2(1920, 720);
		}

		private void OnEnable()
		{
			_assetLibraries = AssetDatabase.FindAssets($"t:{typeof(AssetLibrary)}")
				.Select(AssetDatabase.GUIDToAssetPath)
				.Select(AssetDatabase.LoadAssetAtPath<AssetLibrary>)
				.Where(asset => asset != null).ToList();
		}

		private void Refresh()
		{
			Debug.Log($"Found {_assetLibraries.Count} asset libraries:");
			foreach (var assetLibrary in _assetLibraries) {
				Debug.Log($"{assetLibrary.Name}: {assetLibrary._dbPath}");
			}
		}
	}
}
