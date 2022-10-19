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
using NLog;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	public partial class AssetBrowser : EditorWindow, IDragHandler
	{
		[SerializeField]
		private int _thumbnailSize = 150;

		[SerializeField]
		public string ActiveLibraryForCategories;

		[NonSerialized]
		public List<AssetLibrary> Libraries;

		public IEnumerable<Asset> SelectedAssets => _selectedResults.Select(r => r.Asset);

		[SerializeField]
		private List<string> _selectedLibraries;

		[NonSerialized]
		private List<AssetResult> _assetResults;

		[NonSerialized]
		public AssetQuery Query;

		public const string ThumbPath = "Packages/org.visualpinball.unity.assetlibrary/Editor/Thumbnails~";
		public const int ThumbSize = 256;

		private AssetResult LastSelectedResult {
			set => _detailsElement.Asset = value?.Asset;
		}

		private AssetResult _firstSelectedResult;
		private readonly HashSet<AssetResult> _selectedResults = new();

		private readonly Dictionary<Asset, VisualElement> _elementByAsset = new();
		private readonly Dictionary<VisualElement, AssetResult> _resultByElement = new();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[MenuItem("Visual Pinball/Asset Browser")]
		public static void ShowWindow()
		{
			var wnd = GetWindow<AssetBrowser>();
			wnd.titleContent = new GUIContent("Asset Browser", Icons.AssetLibrary(IconSize.Small));

			// Limit size of the window
			wnd.minSize = new Vector2(640, 240);
		}

		#region Data

		private void Refresh()
		{
			_statusLabel.text = "Loading Assets...";
			RefreshLibraries();
			RefreshCategories();
			RefreshAssets();
		}

		private void RefreshLibraries()
		{
			var selectedLibraries = _selectedLibraries == null ? null : new HashSet<string>(_selectedLibraries);

			if (Libraries != null) {
				foreach (var lib in Libraries) {
					lib.OnChange -= OnLibraryChanged;
				}
			}

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
			Query = new AssetQuery(Libraries.Where(lib => lib.IsActive).ToList());
			Query.OnQueryUpdated += OnQueryUpdated;

			// update left column and subscribe
			_libraryList.Clear();
			foreach (var lib in Libraries) {
				lib.IsActive = selectedLibraries?.Contains(lib.Id) ?? true;
				_libraryList.Add(NewAssetLibrary(lib));
				lib.OnChange += OnLibraryChanged;
			}
		}

		private void OnLibraryChanged(object sender, EventArgs e)
		{
			RefreshLibraries();
			RefreshCategories();
		}

		public void FilterByAttribute(string attributeKey, string value, bool remove = false)
		{
			var queryString = attributeKey.Contains(" ") ? $"\"{attributeKey}\":" : $"{attributeKey}:";
			var queryValue = AssetQuery.ValueToQuery(value);
			queryString += value.Contains(" ") ? $"\"{queryValue}\"" : queryValue;

			if (remove) {
				_queryInput.value = _queryInput.value.Replace(queryString, "").Trim();

			} else {
				_queryInput.value = $"{_queryInput.value} {queryString}".Trim();
			}
		}

		public void FilterByTag(string tag, bool remove = false)
		{
			_queryInput.value = remove
				? _queryInput.value.Replace($"[{tag}]", "").Trim()
				: $"{_queryInput.value} [{tag}]".Trim();
		}

		public void FilterByQuality(AssetQuality quality, bool remove = false)
		{
			_queryInput.value = remove
				? _queryInput.value.Replace($"({quality.ToString()})", "").Trim()
				: $"{_queryInput.value} ({quality.ToString()})".Trim();
		}


		private void RefreshCategories()
		{
			_categoryView.Refresh(this);
		}

		private void RefreshAssets()
		{
			Query.Search(_queryInput.value);
		}

		private void OnQueryUpdated(object sender, AssetQueryResult e)
		{
			UpdateQueryResults(e.Rows, e.DurationMs);
		}

		private void UpdateQueryResults(List<AssetResult> results, long duration)
		{
			_assetResults = results;
			_gridContent.Clear();
			_elementByAsset.Clear();
			_resultByElement.Clear();
			_selectedResults.Clear();

			LastSelectedResult = null;
			foreach (var row in results) {
				var element = NewItem(row);
				_elementByAsset[row.Asset] = element;
				_resultByElement[element] = row;
				_gridContent.Add(_elementByAsset[row.Asset]);
			}

			if (!results.Contains(_firstSelectedResult)) {
				_firstSelectedResult = null;
			} else {
				SelectOnly(_firstSelectedResult);
			}

			_statusLabel.text = $"Found {results.Count} asset" + (results.Count == 1 ? "" : "s") + $" in {duration}ms.";
		}

		private void AddAssetContextMenu(ContextualMenuPopulateEvent evt)
		{
			if (evt.target is VisualElement ve && _resultByElement.ContainsKey(ve)) {
				var clickedAsset = _resultByElement[ve];
				var lib = _resultByElement[ve].Asset.Library;
				if (!lib.IsLocked) {
					evt.menu.AppendAction("Remove from Library", _ => {
						if (!_selectedResults.Contains(clickedAsset)) {
							_selectedResults.Add(clickedAsset);
							ToggleSelectionClass(_elementByAsset[clickedAsset.Asset]);
						}
						var numRemovedAssets = 0;
						foreach (var asset in _selectedResults.Where(a => !a.Asset.Library.IsLocked).ToList()) {
							_selectedResults.Remove(asset);
							asset.Asset.Library.RemoveAsset(asset.Asset);
							numRemovedAssets++;
						}

						RefreshCategories();
						RefreshAssets();
						_statusLabel.text = $"Removed {numRemovedAssets} assets from library.";
					});
				}
			}
		}

		private void OnEmptyClicked(PointerUpEvent evt)
		{
			SelectNone();
		}

		private void OnAssetClicked(IMouseEvent evt, VisualElement element)
		{
			// only interested in left click here
			if (evt.button != 0) {
				return;
			}
			var clickedAsset = _resultByElement[element];

			// no modifier pressed
			if (!evt.shiftKey && !evt.ctrlKey) {
				// already selected?
				if (_selectedResults.Contains(clickedAsset)) {
					if (_selectedResults.Count != 1) {
						SelectOnly(clickedAsset);
					} // if count is 1, and user clicks on it, do nothing.
				} else {
					SelectOnly(clickedAsset);
				}
			}

			// only CTRL pressed
			if (!evt.shiftKey && evt.ctrlKey) {
				// already selected?
				if (_selectedResults.Contains(clickedAsset)) {
					UnSelect(clickedAsset);
				} else {
					Select(clickedAsset);
				}
			}

			// only SHIFT pressed
			if (evt.shiftKey && !evt.ctrlKey) {
				var startIndex = _firstSelectedResult != null ? _assetResults.IndexOf(_firstSelectedResult) : 0;
				var endIndex = _assetResults.IndexOf(clickedAsset);
				LastSelectedResult = clickedAsset;
				SelectRange(startIndex, endIndex);
			}


			// both SHIFT and CTRL pressed
			if (evt.shiftKey && evt.ctrlKey) {
				// todo
			}
		}

		public void OnCategoriesUpdated(Dictionary<AssetLibrary, List<AssetCategory>> categories) => Query.Filter(categories);
		private void OnSearchQueryChanged(ChangeEvent<string> evt) => Query.Search(evt.newValue);
		private void OnLibraryToggled(AssetLibrary lib, bool enabled)
		{
			lib.IsActive = enabled;
			Query.Toggle(lib);
			_selectedLibraries = Libraries.Where(l => l.IsActive).Select(l => l.Id).ToList();
			RefreshCategories();
		}

		public AssetLibrary GetLibraryByPath(string pathToCheck)
		{
			pathToCheck = pathToCheck.Replace('\\', '/');
			return Libraries.FirstOrDefault(assetLibrary => {
				var libraryPath = assetLibrary.LibraryRoot.Replace('\\', '/');
				return pathToCheck.StartsWith(libraryPath);
			});
		}

		public void AddAssets(IEnumerable<string> paths, Func<AssetLibrary, AssetCategory> getCategory)
		{
			var numAdded = 0;
			var numUpdated = 0;
			AssetLibrary updatedLibrary = null;
			foreach (var path in paths) {
				var assetLibrary = GetLibraryByPath(path);
				if (assetLibrary == null) {
					continue;
				}
				var obj = AssetDatabase.LoadAssetAtPath(path, AssetDatabase.GetMainAssetTypeAtPath(path));
				var category = getCategory(assetLibrary);

				if (assetLibrary.AddAsset(obj, category)) {
					Logger.Debug($"{Path.GetFileName(path)} added to library {assetLibrary.Name}.");
					numAdded++;
				} else {
					Logger.Debug($"{Path.GetFileName(path)} updated in library {assetLibrary.Name}.");
					numUpdated++;
				}
				updatedLibrary = assetLibrary;
			}

			_categoryView.Refresh(this);

			if (numAdded > 0 && numUpdated == 0) {
				_statusLabel.text = $"{numAdded} asset" + (numAdded == 1 ? "" : "s") + $" added to library <i>{updatedLibrary!.Name}</i>.";
			} else if (numAdded == 0 && numUpdated > 0) {
				_statusLabel.text = $"{numUpdated} asset" + (numUpdated == 1 ? "" : "s") + $" updated in library <i>{updatedLibrary!.Name}</i>.";
			} else if (numAdded > 0 && numUpdated > 0) {
				_statusLabel.text = $"{numAdded} asset" + (numAdded == 1 ? "" : "s") + $" added and {numUpdated} asset" + (numUpdated == 1 ? "" : "s") + $" updated in library <i>{updatedLibrary!.Name}</i>.";
			} else {
				_statusLabel.text = "No assets added to library.";
			}
		}

		#endregion

		#region Selection

		private void SelectRange(int start, int end)
		{
			if (start > end) {
				(start, end) = (end, start);
			}
			for (var i = 0; i < _assetResults.Count; i++) {
				var asset = _assetResults[i];
				if (i >= start && i <= end) {
					if (!_selectedResults.Contains(asset)) {
						_selectedResults.Add(asset);
						ToggleSelectionClass(_elementByAsset[asset.Asset]);
					}
				} else if (_selectedResults.Contains(asset)) {
					_selectedResults.Remove(asset);
					ToggleSelectionClass(_elementByAsset[asset.Asset]);
				}
			}
		}

		private void SelectNone()
		{
			foreach (var selectedAsset in _selectedResults) {
				ToggleSelectionClass(_elementByAsset[selectedAsset.Asset]);
			}
			_selectedResults.Clear();
			_firstSelectedResult = null;
			LastSelectedResult = null;
		}

		private void SelectOnly(AssetResult result)
		{
			var wasAlreadySelected = false;
			foreach (var selectedAsset in _selectedResults) {
				if (selectedAsset != result) {
					ToggleSelectionClass(_elementByAsset[selectedAsset.Asset]);
				} else {
					wasAlreadySelected = true;
				}
			}
			_selectedResults.Clear();
			_selectedResults.Add(result);
			if (!wasAlreadySelected) {
				ToggleSelectionClass(_elementByAsset[result.Asset]);
			}
			_firstSelectedResult = result;
			LastSelectedResult = result;
		}

		private void UnSelect(AssetResult result)
		{
			_selectedResults.Remove(result);
			ToggleSelectionClass(_elementByAsset[result.Asset]);
			_firstSelectedResult = _selectedResults.Count > 0 ? _selectedResults.FirstOrDefault() : null;
			LastSelectedResult = _selectedResults.Count > 0 ? _selectedResults.LastOrDefault() : null;
		}


		private void Select(AssetResult result)
		{
			_selectedResults.Add(result);
			ToggleSelectionClass(_elementByAsset[result.Asset]);
			LastSelectedResult = result;
		}

		private static void ToggleSelectionClass(VisualElement element) => element.ToggleInClassList("selected");

		#endregion Selection

		#region Drag and Drop

		private static void StartDraggingAssets(HashSet<AssetResult> data) => DragAndDrop.SetGenericData("assets", data);
		public static void StopDraggingAssets() => DragAndDrop.SetGenericData("assets", null);

		public static bool IsDraggingExistingAssets => DragAndDrop.GetGenericData("assets") is HashSet<AssetResult>;

		public static bool IsDraggingNewAssets => DragAndDrop.paths is { Length: > 0 };
		public IEnumerable<AssetResult> NonActiveSelection => !_detailsElement.HasAsset
			? Array.Empty<AssetResult>()
			: _selectedResults.Where(sr => sr.Asset.GUID != _detailsElement.Asset.GUID);

		private void OnDragEnterEvent(DragEnterEvent evt)
		{
			DragError = null;

			if (!IsDraggingNewAssets) {
				return;
			}

			if (_categoryView.NumCategories == 0) {
				DragError = "Unknown category. Seems there are no categories in the database, so you'll need to create one first.";
				return;
			}

			if (_categoryView.NumSelectedCategories != 1) {
				DragError = "Unknown category. You have to filter by one single category when dragging into the grid view. But you can also drag onto the category directly on the left directly.";
				return;
			}

			foreach (var path in DragAndDrop.paths) {
				var assetLibrary = GetLibraryByPath(path);

				if (assetLibrary == null) {
					DragError = "Unknown library. Your assets must be under the root of a library, and at least one of the assets you're dragging is not.";
					break;
				}

				if (assetLibrary.IsLocked) {
					DragError = "Access Error. The library you're trying to add assets to is locked.";
					break;
				}
			}
		}

		private static void OnDragUpdatedEvent(DragUpdatedEvent evt)
		{
			DragAndDrop.visualMode = IsDraggingNewAssets ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
		}

		private void OnDragLeaveEvent(DragLeaveEvent evt)
		{
			// hide error panel
			DragError = null;
		}

		private void OnDragPerformEvent(DragPerformEvent evt)
		{
			if (DragError != null) {
				DragError = null;
				return;
			}

			DragAndDrop.AcceptDrag();
			AddAssets(DragAndDrop.paths, assetLibrary => _categoryView.GetOrCreateSelected(assetLibrary));
		}

		#endregion

		public void RefreshThumb(Asset asset)
		{
			if (!_thumbCache.ContainsKey(asset.GUID)) {
				return;
			}
			_thumbCache.Remove(asset.GUID);
			if (_elementByAsset.ContainsKey(asset)) {
				LoadThumb(_elementByAsset[asset], asset);
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

		public void AttachData(AssetResult clickedResult)
		{
			if (!_selectedResults.Contains(clickedResult)) {
				_selectedResults.Add(clickedResult);
			}
			DragAndDrop.objectReferences = _selectedResults.Select(result => result.Asset.Object).ToArray();
			StartDraggingAssets(_selectedResults);
		}
	}

	public interface IDragHandler
	{
		void AttachData(AssetResult clickedResult);
	}

}
