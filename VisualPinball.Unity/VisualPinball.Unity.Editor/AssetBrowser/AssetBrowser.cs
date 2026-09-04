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
		private readonly Dictionary<AssetLibrary, string> _libraryAssetPaths = new();
		private readonly HashSet<AssetLibrary> _pendingLibraryReindexes = new();
		private bool _fullLibraryRefreshPending;

		[NonSerialized]
		private List<AssetResult> _assetResults;

		[NonSerialized]
		public AssetQuery Query;

		private AssetResult LastSelectedResult {
			set => _detailsElement.Asset = value?.Asset;
		}

		private AssetResult _firstSelectedResult;
		private readonly HashSet<AssetResult> _selectedResults = new();

		private readonly Dictionary<AssetResult, VisualElement> _visibleElements = new();
		private readonly List<int> _gridRowStarts = new();
		private int _gridColumnCount = 1;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[MenuItem("Pinball/Asset Browser")]
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
				.Where(lib => lib != null).ToList();
			_libraryAssetPaths.Clear();
			foreach (var lib in Libraries) {
				lib.IsActive = selectedLibraries?.Contains(lib.Id) ?? true;
				_libraryAssetPaths[lib] = NormalizeAssetPath(AssetDatabase.GetAssetPath(lib));
			}

			// toggle label
			if (Libraries.Count == 0) {
				_noLibrariesLabel.RemoveFromClassList("hidden");
			} else {
				_noLibrariesLabel.AddToClassList("hidden");
			}

			// setup query
			if (Query != null) {
				Query.OnQueryUpdated -= OnQueryUpdated;
			}
			Query = new AssetQuery(Libraries.Where(lib => lib.IsActive).ToList());
			Query.OnQueryUpdated += OnQueryUpdated;

			// update left column and subscribe
			_libraryList.Clear();
			foreach (var lib in Libraries) {
				_libraryList.Add(NewAssetLibrary(lib));
				lib.OnChange += OnLibraryChanged;
			}
		}

		private void OnLibraryChanged(object sender, EventArgs e)
		{
			if (sender is AssetLibrary library) {
				ScheduleLibraryReindex(library);
			} else {
				ScheduleFullLibraryRefresh();
			}
		}

		private void OnAssetFilesChanged(string[] paths)
		{
			var normalizedPaths = paths.Select(NormalizeAssetPath).ToArray();
			var libraries = (Libraries ?? Enumerable.Empty<AssetLibrary>()).ToArray();
			var databaseRoots = libraries.ToDictionary(library => library, library => NormalizeAssetPath(library.DatabaseRoot));
			if (normalizedPaths.Any(path => _libraryAssetPaths.Values.Contains(path))) {
				ScheduleFullLibraryRefresh();
				return;
			}
			var pathsOutsideDatabases = normalizedPaths
				.Where(path => databaseRoots.Values.All(root => !IsPathWithin(path, root)));
			if (pathsOutsideDatabases.Any(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(AssetLibrary))) {
				ScheduleFullLibraryRefresh();
				return;
			}
			foreach (var library in libraries) {
				if (normalizedPaths.Any(path => IsPathWithin(path, databaseRoots[library]))) {
					ScheduleLibraryReindex(library);
				}
			}
		}

		private void ScheduleLibraryReindex(AssetLibrary library)
		{
			if (library != null) {
				_pendingLibraryReindexes.Add(library);
			}
			ScheduleLibraryRefresh();
		}

		private void ScheduleFullLibraryRefresh()
		{
			_fullLibraryRefreshPending = true;
			ScheduleLibraryRefresh();
		}

		private void ScheduleLibraryRefresh()
		{
			_libraryRefreshScheduledItem?.Pause();
			_libraryRefreshScheduledItem = rootVisualElement.schedule.Execute(ApplyPendingLibraryChanges).StartingIn(50);
		}

		private void ApplyPendingLibraryChanges()
		{
			if (_fullLibraryRefreshPending) {
				_fullLibraryRefreshPending = false;
				_pendingLibraryReindexes.Clear();
				Refresh();
				return;
			}
			var libraries = _pendingLibraryReindexes.ToArray();
			_pendingLibraryReindexes.Clear();
			foreach (var library in libraries.Where(library => Libraries.Contains(library))) {
				Query.Reindex(library);
			}
			RefreshCategories();
			RefreshAssets();
		}

		private static string NormalizeAssetPath(string path) => (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');

		private static bool IsPathWithin(string path, string root) => root.Length > 0
			&& (string.Equals(path, root, StringComparison.Ordinal) || path.StartsWith(root + "/", StringComparison.Ordinal));

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
			_categoryView.UpdateCategoryTags();
		}

		private void UpdateQueryResults(List<AssetResult> results, long duration)
		{
			_assetResults = results;
			_selectedResults.Clear();

			LastSelectedResult = null;
			RebuildGridRows(true);

			if (!results.Contains(_firstSelectedResult)) {
				_firstSelectedResult = null;
			} else {
				SelectOnly(_firstSelectedResult);
			}

			_statusLabel.text = $"Found {results.Count} asset" + (results.Count == 1 ? "" : "s") + $" in {duration}ms.";
		}

			private void AddAssetContextMenu(ContextualMenuPopulateEvent evt, AssetResult clickedAsset)
			{
				if (clickedAsset == null) {
					return;
				}
				var libs = clickedAsset.Asset.Libraries;
				if (libs.All(l => l.IsLocked)) {
					Debug.Log("Early out in AddAssetContextMenu, all libraries are locked.");
					return;
				}

				// lib is not locked, and asset is known.
				foreach (var lib in libs.Where(l => !l.IsLocked)) {
					evt.menu.AppendAction($"Remove from {lib.Name}", _ => {
						if (_selectedResults.Add(clickedAsset)) {
							UpdateSelectionClass(clickedAsset);
						}
						var numRemovedAssets = 0;
						foreach (var assetResult in _selectedResults.Where(a => lib.HasAsset(a.Asset.GUID)).ToList()) {
							_selectedResults.Remove(assetResult);
							lib.DeleteAsset(assetResult.Asset);
							if (libs.Length == 1) {
								RemoveCachedThumbnails(assetResult.Asset.GUID);
							}
							numRemovedAssets++;
						}

						RefreshCategories();
						RefreshAssets();
						_statusLabel.text = $"Removed {numRemovedAssets} asset(s) from library {lib.Name}.";
					});
				}

				if (_selectedResults.Count > 1) {
					var srcAsset = clickedAsset.Asset;
					evt.menu.AppendSeparator();
					evt.menu.AppendAction("Add Attributes to Selected", _ => {
						var destAssets = OtherSelected(clickedAsset).ToList();
						foreach (var destAsset in destAssets) {
							foreach (var attr in srcAsset.Attributes) {
								destAsset.AddAttribute(attr.Key, attr.Value);
							}
							destAsset.Save();
						}
						EditorUtility.DisplayDialog("Add Attributes to Selected", $"Added {srcAsset.Attributes.Count} attributes to {destAssets.Count} other assets.", "OK");

					});
					evt.menu.AppendAction("Replace Attributes in Selected", _ => {
						var destAssets = OtherSelected(clickedAsset).ToList();
						foreach (var destAsset in destAssets) {
							foreach (var attr in srcAsset.Attributes) {
								destAsset.ReplaceAttribute(attr.Key, attr.Value);
							}
							destAsset.Save();
						}
						EditorUtility.DisplayDialog("Replace Attributes to Selected", $"Replaced {srcAsset.Attributes.Count} attributes in {destAssets.Count} other assets.", "OK");
					});

					evt.menu.AppendSeparator();
					evt.menu.AppendAction("Add Tags to Selected", _ => {
						var destAssets = OtherSelected(clickedAsset).ToList();
						foreach (var destAsset in destAssets) {
							foreach (var tag in srcAsset.Tags) {
								destAsset.AddTag(tag.TagName);
							}
							destAsset.Save();
						}
						EditorUtility.DisplayDialog("Add Tags to Selected", $"Added {srcAsset.Tags.Count} tags to {destAssets.Count} other assets.", "OK");
					});

					evt.menu.AppendAction("Replace Tags in Selected", _ => {
						var destAssets = OtherSelected(clickedAsset).ToList();
						foreach (var destAsset in destAssets) {
							destAsset.Tags.Clear();
							foreach (var tag in srcAsset.Tags) {
								destAsset.AddTag(tag.TagName);
							}
							destAsset.Save();
						}
						EditorUtility.DisplayDialog("Replace Tags in Selected", $"Replaced {srcAsset.Tags.Count} tags in {destAssets.Count} other assets.", "OK");
					});

					evt.menu.AppendSeparator();
					evt.menu.AppendAction("Copy All to Selected", _ => {
						var destAssets = OtherSelected(clickedAsset).ToList();
						foreach (var destAsset in destAssets) {
							if (string.IsNullOrEmpty(destAsset.Description)) {
								destAsset.Description = srcAsset.Description;
							}
							foreach (var tag in srcAsset.Tags) {
								destAsset.AddTag(tag.TagName);
							}
							foreach (var attr in srcAsset.Attributes) {
								destAsset.AddAttribute(attr.Key, attr.Value);
							}
							foreach (var link in srcAsset.Links.Where(link => destAsset.Links.All(l => l.Name != link.Name))) {
								destAsset.Links.Add(new AssetLink(link.Name, link.Url));
							}

							destAsset.Quality = srcAsset.Quality;
							destAsset.ThumbCameraPreset = srcAsset.ThumbCameraPreset;

							destAsset.Save();
						}
						EditorUtility.DisplayDialog("Copy All to Selected", $"Copied data of to {destAssets.Count} other assets.", "OK");
					});
				}
			}

		private IEnumerable<Asset> OtherSelected(AssetResult src)
		{
			return _selectedResults.Where(a => !a.Asset.Library.IsLocked && a.Asset.GUID != src.Asset.GUID).Select(ar => ar.Asset);
		}

		private void OnEmptyClicked(PointerUpEvent evt)
		{
			SelectNone();
		}

		private void OnAssetClicked(ClickEvent evt, VisualElement element)
		{
			// only interested in left click here
			if (evt.button != 0) {
				return;
			}
			if (element.userData is not AssetResult clickedAsset) {
				return;
			}

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
		private void OnSearchQueryChanged(ChangeEvent<string> evt)
		{
			_pendingSearch = evt.newValue;
			_searchScheduledItem?.Pause();
			_searchScheduledItem = _queryInput.schedule.Execute(() => Query?.Search(_pendingSearch)).StartingIn(200);
		}
		private void OnLibraryToggled(AssetLibrary lib, bool enabled)
		{
			lib.IsActive = enabled;
			Query.Toggle(lib);
			_selectedLibraries = Libraries.Where(l => l.IsActive).Select(l => l.Id).ToList();
			RefreshCategories();
		}

		public AssetLibrary GetLibraryByPath(string pathToCheck)
		{
			pathToCheck = pathToCheck.Replace('\\', '/').TrimEnd('/');
			return Libraries
				.OrderByDescending(assetLibrary => assetLibrary.LibraryRoot.Length)
				.FirstOrDefault(assetLibrary => {
					var libraryPath = assetLibrary.LibraryRoot.Replace('\\', '/').TrimEnd('/');
					return string.Equals(pathToCheck, libraryPath, StringComparison.Ordinal)
					       || pathToCheck.StartsWith(libraryPath + "/", StringComparison.Ordinal);
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
						UpdateSelectionClass(asset);
					}
				} else if (_selectedResults.Contains(asset)) {
					_selectedResults.Remove(asset);
					UpdateSelectionClass(asset);
				}
			}
		}

		private void SelectNone()
		{
			foreach (var selectedAsset in _selectedResults) {
				UpdateSelectionClass(selectedAsset, false);
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
					UpdateSelectionClass(selectedAsset, false);
				} else {
					wasAlreadySelected = true;
				}
			}
			_selectedResults.Clear();
			_selectedResults.Add(result);
			if (!wasAlreadySelected) {
				UpdateSelectionClass(result, true);
			}
			_firstSelectedResult = result;
			LastSelectedResult = result;
		}

		private void UnSelect(AssetResult result)
		{
			_selectedResults.Remove(result);
			UpdateSelectionClass(result, false);
			_firstSelectedResult = _selectedResults.Count > 0 ? _selectedResults.FirstOrDefault() : null;
			LastSelectedResult = _selectedResults.Count > 0 ? _selectedResults.LastOrDefault() : null;
		}


		private void Select(AssetResult result)
		{
			_selectedResults.Add(result);
			UpdateSelectionClass(result, true);
			LastSelectedResult = result;
		}

		private void UpdateSelectionClass(AssetResult result, bool? selected = null)
		{
			if (_visibleElements.TryGetValue(result, out var element)) {
				element.EnableInClassList("selected", selected ?? _selectedResults.Contains(result));
			}
		}

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
			RemoveCachedThumbnails(asset.GUID);
			var visibleResult = _visibleElements.Keys.FirstOrDefault(result => result.Asset == asset);
			if (visibleResult != null) {
				LoadThumb(_visibleElements[visibleResult], asset);
			}
		}

		private void OnThumbSizeChanged(ChangeEvent<float> evt)
		{
			_thumbnailSize = (int)evt.newValue;
			RebuildGridRows();
		}

		public void AttachData(AssetResult clickedResult)
		{
			_selectedResults.Add(clickedResult);
			UpdateSelectionClass(clickedResult, true);
			DragAndDrop.objectReferences = _selectedResults.Select(result => result.Asset.Object).ToArray();
			StartDraggingAssets(_selectedResults);
		}
	}

	public interface IDragHandler
	{
		void AttachData(AssetResult clickedResult);
	}

}
