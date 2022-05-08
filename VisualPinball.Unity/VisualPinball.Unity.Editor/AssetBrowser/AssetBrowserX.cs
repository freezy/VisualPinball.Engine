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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public partial class AssetBrowserX : EditorWindow, IDragHandler
	{
		[SerializeField]
		private int _thumbnailSize = 150;

		[SerializeField]
		public string ActiveLibraryForCategories;

		[NonSerialized]
		public List<AssetLibrary> Libraries;

		[NonSerialized]
		private List<AssetData> _assets;

		[NonSerialized]
		private string _dragError;

		[NonSerialized]
		public AssetQuery Query;

		private AssetData LastSelectedAsset {
			set => _detailsElement.Asset = value;
		}

		private AssetData _firstSelectedAsset;
		private readonly HashSet<AssetData> _selectedAssets = new();

		private readonly Dictionary<AssetData, VisualElement> _elementByAsset = new();
		private readonly Dictionary<VisualElement, AssetData> _assetsByElement = new();

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

			// toggle label
			if (Libraries.Count == 0) {
				_noLibrariesLabel.RemoveFromClassList("hidden");
			} else {
				_noLibrariesLabel.AddToClassList("hidden");
			}

			// setup query
			Query = new AssetQuery(Libraries);
			Query.OnQueryUpdated += OnQueryUpdated;

			// update left column
			_libraryList.Clear();
			foreach (var assetLibrary in Libraries) {
				_libraryList.Add(NewAssetLibrary(assetLibrary));
			}
		}

		public void FilterByAttribute(string attributeKey, string value)
		{
			_queryInput.value = attributeKey.Contains(" ")
				? $"{_queryInput.value} \"{attributeKey}\":".Trim()
				: $"{_queryInput.value} {attributeKey}:".Trim();

			if (value.Contains(" ")) {
				_queryInput.value += $"\"{value}\"".Trim();
			} else {
				_queryInput.value += $"{value}".Trim();
			}
		}

		private void RefreshCategories()
		{
			_categoryView.Refresh(this);
		}

		private void RefreshAssets()
		{
			Query.Run();
		}

		private void OnQueryUpdated(object sender, AssetQueryResult e)
		{
			UpdateQueryResults(e.Rows);
		}

		private void UpdateQueryResults(List<AssetData> assets)
		{
			_bottomLabel.text = $"Found {assets.Count} asset" + (assets.Count == 1 ? "" : "s") + ".";
			_assets = assets;
			_gridContent.Clear();
			_elementByAsset.Clear();
			_assetsByElement.Clear();
			_selectedAssets.Clear();
			_firstSelectedAsset = null;
			LastSelectedAsset = null;
			foreach (var row in assets) {
				var element = NewItem(row);
				_elementByAsset[row] = element;
				_assetsByElement[element] = row;
				_gridContent.Add(_elementByAsset[row]);
			}
		}

		private void OnEmptyClicked(PointerUpEvent evt)
		{
			SelectNone();
		}

		private void OnItemClicked(IMouseEvent evt, VisualElement element)
		{
			var clickedAsset = _assetsByElement[element];

			// no modifier pressed
			if (!evt.shiftKey && !evt.ctrlKey) {
				// already selected?
				if (_selectedAssets.Contains(clickedAsset)) {
					if (_selectedAssets.Count != 1) {
						SelectOnly(clickedAsset);
					} // if count is 1, and user clicks on it, do nothing.
				} else {
					SelectOnly(clickedAsset);
				}
			}

			// only CTRL pressed
			if (!evt.shiftKey && evt.ctrlKey) {
				// already selected?
				if (_selectedAssets.Contains(clickedAsset)) {
					UnSelect(clickedAsset);
				} else {
					Select(clickedAsset);
				}
			}

			// only SHIFT pressed
			if (evt.shiftKey && !evt.ctrlKey) {
				var startIndex = _firstSelectedAsset != null ? _assets.IndexOf(_firstSelectedAsset) : 0;
				var endIndex = _assets.IndexOf(clickedAsset);
				LastSelectedAsset = clickedAsset;
				SelectRange(startIndex, endIndex);
			}


			// both SHIFT and CTRL pressed
			if (evt.shiftKey && evt.ctrlKey) {
				// todo
			}
		}

		#region Selection

		private void SelectRange(int start, int end)
		{
			if (start > end) {
				(start, end) = (end, start);
			}
			for (var i = 0; i < _assets.Count; i++) {
				var asset = _assets[i];
				if (i >= start && i <= end) {
					if (!_selectedAssets.Contains(asset)) {
						_selectedAssets.Add(asset);
						ToggleSelectionClass(_elementByAsset[asset]);
					}
				} else if (_selectedAssets.Contains(asset)) {
					_selectedAssets.Remove(asset);
					ToggleSelectionClass(_elementByAsset[asset]);
				}
			}
		}

		private void SelectNone()
		{
			foreach (var selectedAsset in _selectedAssets) {
				ToggleSelectionClass(_elementByAsset[selectedAsset]);
			}
			_selectedAssets.Clear();
			_firstSelectedAsset = null;
			LastSelectedAsset = null;
		}

		private void SelectOnly(AssetData asset)
		{
			var wasAlreadySelected = false;
			foreach (var selectedAsset in _selectedAssets) {
				if (selectedAsset != asset) {
					ToggleSelectionClass(_elementByAsset[selectedAsset]);
				} else {
					wasAlreadySelected = true;
				}
			}
			_selectedAssets.Clear();
			_selectedAssets.Add(asset);
			if (!wasAlreadySelected) {
				ToggleSelectionClass(_elementByAsset[asset]);
			}
			_firstSelectedAsset = asset;
			LastSelectedAsset = asset;
		}

		private void UnSelect(AssetData asset)
		{
			_selectedAssets.Remove(asset);
			ToggleSelectionClass(_elementByAsset[asset]);
			_firstSelectedAsset = _selectedAssets.Count > 0 ? _selectedAssets.FirstOrDefault() : null;
			LastSelectedAsset = _selectedAssets.Count > 0 ? _selectedAssets.LastOrDefault() : null;
		}


		private void Select(AssetData asset)
		{
			_selectedAssets.Add(asset);
			ToggleSelectionClass(_elementByAsset[asset]);
			LastSelectedAsset = asset;
		}

		private static void ToggleSelectionClass(VisualElement element) => element.ToggleInClassList("selected");

		#endregion Selection

		public void OnCategoriesUpdated(Dictionary<AssetLibrary, List<LibraryCategory>> categories) => Query.Filter(categories);
		private void OnSearchQueryChanged(ChangeEvent<string> evt) => Query.Search(evt.newValue);
		private void OnLibraryToggled(AssetLibrary lib, bool enabled) => Query.Toggle(lib, enabled);

		private void OnDragEnterEvent(DragEnterEvent evt)
		{
			_dragError = null;

			if (_categoryView.NumCategories == 0) {
				_dragError = "Unknown category. Seems there are no categories in the database, so you'll need to create one first.";

			} else if (_categoryView.NumSelectedCategories != 1) {
				_dragError = "Unknown category. You have to filter by one single category when dragging into the grid view. But you can also drag onto the category directly on the left directly.";

			} else {
				foreach (var path in DragAndDrop.paths) {
					foreach (var assetLibrary in Libraries) {
						if (path.Replace('\\', '/').StartsWith(assetLibrary.LibraryRoot.Replace('\\', '/'))) {
							if (!assetLibrary.IsReadOnly) {
								continue;
							}
							_dragError = "Access Error. The library you're trying to add assets to is in read-only mode.";
							break;
						}

						_dragError = "Unknown library. Your assets must be under the root of a library, and at least one of the assets you're dragging is not.";
						break;
					}

					if (_dragError != null) {
						break;
					}
				}
			}

			if (_dragError != null) {
				// show error panel
				_dragErrorContainer.RemoveFromClassList("hidden");
				_dragErrorLabel.text = _dragError;
			}
		}

		private void OnDragUpdatedEvent(DragUpdatedEvent evt)
		{
			DragAndDrop.visualMode = _dragError != null
				? DragAndDropVisualMode.Rejected
				: DragAndDropVisualMode.Copy;
		}

		private void OnDragLeaveEvent(DragLeaveEvent evt)
		{
			// hide error panel
			_dragErrorContainer.AddToClassList("hidden");
		}

		private void OnDragPerformEvent(DragPerformEvent evt)
		{
			if (_dragError != null) {
				// hide error panel
				_dragErrorContainer.AddToClassList("hidden");
				return;
			}

			DragAndDrop.AcceptDrag();

			// Disallow adding from outside of Unity
			foreach (var path in DragAndDrop.paths) {
				foreach (var assetLibrary in Libraries) {
					if (path.Replace('\\', '/').StartsWith(assetLibrary.LibraryRoot.Replace('\\', '/'))) {
						var guid = AssetDatabase.AssetPathToGUID(path);
						var type = AssetDatabase.GetMainAssetTypeAtPath(path);
						var category = _categoryView.GetOrCreateSelected(assetLibrary);

						if (assetLibrary.AddAsset(guid, type, path, category)) {
							Debug.Log($"{Path.GetFileName(path)} added to library {assetLibrary.Name}.");
						} else {
							Debug.Log($"{Path.GetFileName(path)} updated in library {assetLibrary.Name}.");
						}

						// todo update data views
					}
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

		public void AttachData()
		{
			DragAndDrop.objectReferences = _selectedAssets.Select(row => row.Asset.LoadAsset()).ToArray();
			DragAndDrop.SetGenericData("assets", _selectedAssets);
		}
	}

	public interface IDragHandler
	{
		void AttachData();
	}

}
