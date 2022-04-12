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
		private int _thumbnailSize = 150;

		public AssetLibrary ActiveLibrary;
		public List<AssetLibrary> Libraries;

		private List<LibraryAsset> _assets;
		private AssetQuery _query;

		private LibraryAsset _selectedAsset;
		private readonly Dictionary<LibraryAsset, VisualElement> _elementByAsset = new();
		private readonly Dictionary<VisualElement, LibraryAsset> _assetsByElement = new();

		[MenuItem("Visual Pinball/Asset Browser")]
		public static void ShowWindow()
		{
			var wnd = GetWindow<AssetBrowserX>("Asset Browser");

			// Limit size of the window
			wnd.minSize = new Vector2(450, 200);
			wnd.maxSize = new Vector2(1920, 720);
		}

		private void Refresh()
		{
			RefreshLibraries();
			RefreshCategories();
			RefreshAssets();
		}

		private void RefreshLibraries()
		{
			// find library assets
			Libraries = AssetDatabase.FindAssets($"t:{typeof(AssetLibrary)}")
				.Select(AssetDatabase.GUIDToAssetPath)
				.Select(AssetDatabase.LoadAssetAtPath<AssetLibrary>)
				.Where(asset => asset != null).ToList();

			// setup query
			_query = new AssetQuery(Libraries);
			_query.OnQueryUpdated += OnQueryUpdated;

			// update left column
			_libraryList.Clear();
			foreach (var assetLibrary in Libraries) {
				_libraryList.Add(NewAssetLibrary(assetLibrary));
			}

			// update top dropdown
			_activeLibraryDropdown.choices = Libraries.Select(l => l.Name).ToList();
			if (ActiveLibrary != null && Libraries.Count > 0) {
				_activeLibraryDropdown.index = System.Math.Max(0, _activeLibraryDropdown.choices.IndexOf(ActiveLibrary.Name));
			}
		}

		private void RefreshCategories()
		{
			_categoryView.Refresh(this);
		}

		private void RefreshAssets()
		{
			_query.Run();
		}

		private void OnQueryUpdated(object sender, AssetQueryResult e)
		{
			UpdateQueryResults(e.Assets);
		}

		private void UpdateQueryResults(List<LibraryAsset> assets)
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

		public void OnCategoriesUpdated(Dictionary<AssetLibrary, List<LibraryCategory>> categories) => _query.Filter(categories);
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
			// can only drag onto the asset grid if only one category is selected.
			if (_categoryView.NumSelectedCategories != 1) {
				Debug.Log("Only one category must be selected when dragging onto the main asset panel.");
				return;
			}

			DragAndDrop.AcceptDrag();

			// Disallow adding from outside of Unity
			foreach (var path in DragAndDrop.paths) {
				var libraryFound = false;
				foreach (var assetLibrary in Libraries) {
					if (path.Replace('\\', '/').StartsWith(assetLibrary.LibraryRoot.Replace('\\', '/'))) {
						libraryFound = true;
						var guid = AssetDatabase.AssetPathToGUID(path);
						var type = AssetDatabase.GetMainAssetTypeAtPath(path);
						var category = _categoryView.GetOrCreateSelected(assetLibrary);

						if (assetLibrary.AddAsset(guid, type, path, category)) {
							Debug.Log($"{Path.GetFileName(path)} added to library {assetLibrary.Name}.");
						} else {
							Debug.Log($"{Path.GetFileName(path)} updated in library {assetLibrary.Name}.");
						}

						//Setup();
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
		private string OnActiveLibraryChanged(string libraryName)
		{
			if (Libraries == null) {
				return libraryName;
			}
			var library = Libraries.FirstOrDefault(l => l.Name == libraryName);
			if (library != null) {
				ActiveLibrary = library;
			}
			return libraryName;
		}
	}

}
