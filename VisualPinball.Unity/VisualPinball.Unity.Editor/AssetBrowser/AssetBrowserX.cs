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

// ReSharper disable InconsistentNaming

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public partial class AssetBrowserX : EditorWindow
	{
		[SerializeField]
		private int _selectedIndex = -1;

		[SerializeField]
		private int _thumbnailSize = 150;

		private List<LibraryAsset> _assets;
		private List<AssetLibrary> _assetLibraries;
		private AssetQuery _query;

		private LibraryAsset _selectedAsset;
		private readonly Dictionary<LibraryAsset, VisualElement> _elementByAsset = new();
		private readonly Dictionary<VisualElement, LibraryAsset> _assetsByElement = new();
		private List<LibraryCategory> _categories = new();

		[MenuItem("Visual Pinball/Asset Browser")]
		public static void ShowWindow()
		{
			var wnd = GetWindow<AssetBrowserX>("Asset Browser");

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

			_query = new AssetQuery(_assetLibraries);
			_query.OnQueryUpdated += OnResult;
		}

		private void OnResult(object sender, AssetQueryResult e)
		{
			UpdateResults(e.Assets);
		}

		private void UpdateResults(List<LibraryAsset> assets)
		{
			_bottomLabel.text = $"Found {assets.Count} assets.";
			_gridContent.Clear();
			_elementByAsset.Clear();
			_assetsByElement.Clear();
			foreach (var asset in assets) {
				var obj = AssetDatabase.LoadAssetAtPath(asset.Path, TypeByName(asset.Type));
				var tex = AssetPreview.GetAssetPreview(obj);
				var element = NewItem(tex, Path.GetFileNameWithoutExtension(asset.Path));
				_elementByAsset[asset] = element;
				_assetsByElement[element] = asset;
				_gridContent.Add(_elementByAsset[asset]);
			}
		}

		private void Setup()
		{
			OnDestroy();
			rootVisualElement.Clear();
			CreateGUI();
			_libraryList.Clear();
			_categoryView.Refresh(_assetLibraries);
			_selectedAsset = null;
			UpdateResults(_query.Assets);

			foreach (var assetLibrary in _assetLibraries) {
				_libraryList.Add(NewAssetLibrary(assetLibrary));
			}
		}

		private void OnItemClicked(VisualElement element)
		{
			var asset = _assetsByElement[element];
			if (_selectedAsset != null) {
				ToggleSelectionClass(_elementByAsset[_selectedAsset]);
			}
			_selectedAsset = asset;
			ToggleSelectionClass(element);
		}

		private static void ToggleSelectionClass(VisualElement element) => element.ToggleInClassList("selected");

		private void OnSearchQueryChanged(ChangeEvent<string> evt) => _query.Search(evt.newValue);
		private void OnLibraryToggled(AssetLibrary lib, bool enabled) => _query.Toggle(lib, enabled);

		private void OnDragUpdatedEvent(DragUpdatedEvent evt)
		{
			DragAndDrop.visualMode = DragAndDrop.objectReferences != null
				? DragAndDropVisualMode.Move
				: DragAndDropVisualMode.Copy;
		}

		private void OnDragPerformEvent(DragPerformEvent evt)
		{
			DragAndDrop.AcceptDrag();

			// Disallow adding from outside of Unity
			foreach (var path in DragAndDrop.paths) {
				var libraryFound = false;
				foreach (var assetLibrary in _assetLibraries) {
					if (path.Replace('\\', '/').StartsWith(assetLibrary.LibraryRoot.Replace('\\', '/'))) {
						libraryFound = true;
						var guid = AssetDatabase.AssetPathToGUID(path);
						var type = AssetDatabase.GetMainAssetTypeAtPath(path);

						if (assetLibrary.AddAsset(guid, type, path)) {
							Debug.Log($"{Path.GetFileName(path)} added to library {assetLibrary.Name}.");
						} else {
							Debug.Log($"{Path.GetFileName(path)} updated in library {assetLibrary.Name}.");
						}

						Setup();
					}
				}
				if (!libraryFound) {
					Debug.LogError($"Cannot find a VPE library at path {Path.GetDirectoryName(path)}, ignoring asset {Path.GetFileName(path)}.");
				}
			}
		}
		private void OnThumbSizeChanged(ChangeEvent<float> evt)
		{
			_thumbnailSize = (int)evt.newValue;
			foreach (var e in _elementByAsset.Values) {
				e.style.width = _thumbnailSize;
				e.style.height = _thumbnailSize;
			}
		}
	}

}
