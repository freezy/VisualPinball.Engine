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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public partial class AssetBrowser
	{
		private ToolbarButton _refreshButton;
		private ToolbarSearchField _queryInput;

		private LibraryCategoryView _categoryView;
		private VisualElement _libraryList;
		private Label _noLibrariesLabel;

		private ListView _gridContent;
		private Label _dragErrorLabelLeft;
		private VisualElement _dragErrorContainerLeft;
		private Label _dragErrorLabel;
		private VisualElement _dragErrorContainer;
		private AssetDetails _detailsElement;
		private Label _statusLabel;
		private Slider _sizeSlider;

		private VisualTreeAsset _assetTree;
		private StyleSheet _assetStyle;

		private readonly Dictionary<ThumbnailKey, CachedThumbnail> _thumbCache = new();
		private readonly LinkedList<ThumbnailKey> _thumbLru = new();
		private readonly Dictionary<VisualElement, CancellationTokenSource> _thumbnailLoads = new();
		private readonly SemaphoreSlim _thumbnailDecodeSlots = new(4);
		private long _thumbCacheBytes;
		private bool _isDestroyed;
		private IVisualElementScheduledItem _searchScheduledItem;
		private string _pendingSearch;

		private const string ClassDrag = "library-element--dragover";
		private const long ThumbCacheBudget = 256L * 1024L * 1024L;

		public string DragErrorLeft {
			get => _dragErrorContainerLeft.ClassListContains("hidden") ? null : _dragErrorLabelLeft.text;
			set {
				if (value == null) {
					if (!_dragErrorContainerLeft.ClassListContains("hidden")) {
						_dragErrorContainerLeft.AddToClassList("hidden");
					}
					return;
				}

				_dragErrorContainerLeft.RemoveFromClassList("hidden");
				_dragErrorLabelLeft.text = value;
			}
		}

		private string DragError {
			get => _dragErrorContainer.ClassListContains("hidden") ? null : _dragErrorLabel.text;
			set {
				if (value == null) {
					if (!_dragErrorContainer.ClassListContains("hidden")) {
						_dragErrorContainer.AddToClassList("hidden");
					}
					return;
				}

				_dragErrorContainer.RemoveFromClassList("hidden");
				_dragErrorLabel.text = value;
			}
		}

		/// <summary>
		/// Setup the UI. Data is already set up at this point. We'll just trigger a refresh once the UI is set up.
		/// </summary>
		public void CreateGUI()
		{
			// import UXML
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetBrowser.uxml");
			visualTree.CloneTree(rootVisualElement);
			_assetTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryAssetElement.uxml");
			_assetStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/LibraryAssetElement.uss");

			var ui = rootVisualElement;

			// import style sheet
			var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Editor/AssetBrowser/AssetBrowser.uss");
			ui.styleSheets.Add(styleSheet);

			// libraries
			_libraryList = ui.Q<VisualElement>("libraryList");
			_noLibrariesLabel = ui.Q<Label>("noLibraries");

			_categoryView = ui.Q<LibraryCategoryView>();
			_gridContent = ui.Q<ListView>("gridContent");
			_gridContent.styleSheets.Add(_assetStyle);
			_gridContent.makeItem = MakeGridRow;
			_gridContent.bindItem = BindGridRow;
			_gridContent.unbindItem = UnbindGridRow;
			_gridContent.itemsSource = _gridRowStarts;
			_gridContent.selectionType = SelectionType.None;
			_gridContent.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
			UpdateGridMetrics();
			_detailsElement = ui.Q<AssetDetails>();

			_statusLabel = ui.Q<Label>("bottomLabel");
			_sizeSlider = ui.Q<Slider>("sizeSlider");
			_sizeSlider.value = _thumbnailSize;
			_sizeSlider.RegisterValueChangedCallback(OnThumbSizeChanged);

			_refreshButton = ui.Q<ToolbarButton>("refreshButton");
			_refreshButton.clicked += Refresh;

			_queryInput = ui.Q<ToolbarSearchField>("queryInput");
			_queryInput.RegisterValueChangedCallback(OnSearchQueryChanged);

			_dragErrorContainer = ui.Q<VisualElement>("dragErrorContainer");
			_dragErrorLabel = ui.Q<Label>("dragError");

			_dragErrorContainerLeft = ui.Q<VisualElement>("dragErrorContainerLeft");
			_dragErrorLabelLeft = ui.Q<Label>("dragErrorLeft");

			_gridContent.RegisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);
			_gridContent.RegisterCallback<DragPerformEvent>(OnDragPerformEvent);
			_gridContent.RegisterCallback<DragEnterEvent>(OnDragEnterEvent);
			_gridContent.RegisterCallback<DragLeaveEvent>(OnDragLeaveEvent);
			_gridContent.RegisterCallback<PointerUpEvent>(OnEmptyClicked);
			_gridContent.RegisterCallback<KeyDownEvent>(OnGridKeyDown);
			_gridContent.RegisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);

			ui.panel.visualTree.userData = this; // children need access to this. if there's another way of getting the panel's owner object, let me know!

			Refresh();
		}

		private void OnDestroy()
		{
			_isDestroyed = true;
			_searchScheduledItem?.Pause();
			_sizeSlider?.UnregisterValueChangedCallback(OnThumbSizeChanged);
			_queryInput?.UnregisterValueChangedCallback(OnSearchQueryChanged);

			_gridContent?.UnregisterCallback<PointerUpEvent>(OnEmptyClicked);
			_gridContent?.UnregisterCallback<KeyDownEvent>(OnGridKeyDown);
			_gridContent?.UnregisterCallback<GeometryChangedEvent>(OnGridGeometryChanged);
			_gridContent?.UnregisterCallback<DragLeaveEvent>(OnDragLeaveEvent);
			_gridContent?.UnregisterCallback<DragEnterEvent>(OnDragEnterEvent);
			_gridContent?.UnregisterCallback<DragPerformEvent>(OnDragPerformEvent);
			_gridContent?.UnregisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);
			if (_refreshButton != null) {
				_refreshButton.clicked -= Refresh;
			}

			if (Libraries != null) {
				foreach (var assetLibrary in Libraries) {
					assetLibrary.OnChange -= OnLibraryChanged;
				}
			}
			if (Query != null) {
				Query.OnQueryUpdated -= OnQueryUpdated;
			}
			foreach (var load in _thumbnailLoads.Values) {
				load.Cancel();
			}
			_thumbnailLoads.Clear();
			foreach (var thumbnail in _thumbCache.Values) {
				DestroyImmediate(thumbnail.Texture);
			}
			_thumbCache.Clear();
			_thumbLru.Clear();
			_thumbCacheBytes = 0;
		}

		private VisualElement MakeGridRow()
		{
			var row = new VisualElement();
			row.AddToClassList("asset-grid-row");
			return row;
		}

		private void BindGridRow(VisualElement row, int rowIndex)
		{
			EnsureGridCells(row);
			var rowStart = _gridRowStarts[rowIndex];
			for (var column = 0; column < _gridColumnCount; column++) {
				var cell = row[column];
				var resultIndex = rowStart + column;
				if (resultIndex < _assetResults.Count) {
					BindGridCell(cell, _assetResults[resultIndex]);
				} else {
					UnbindGridCell(cell);
				}
			}
		}

		private void UnbindGridRow(VisualElement row, int rowIndex)
		{
			foreach (var cell in row.Children()) {
				UnbindGridCell(cell);
			}
		}

		private void EnsureGridCells(VisualElement row)
		{
			while (row.childCount > _gridColumnCount) {
				var lastCell = row[row.childCount - 1];
				UnbindGridCell(lastCell);
				lastCell.RemoveFromHierarchy();
			}
			while (row.childCount < _gridColumnCount) {
				row.Add(CreateGridCell());
			}
		}

		private VisualElement CreateGridCell()
		{
			var cell = new VisualElement();
			_assetTree.CloneTree(cell);
			var assetElement = cell.Q<LibraryAssetElement>();
			var img = cell.Q<VisualElement>("thumbnail-mask");
			var label = cell.Q<Label>("label");
			label.style.textOverflow = TextOverflow.Ellipsis;
			cell.RegisterCallback<ClickEvent>(evt => OnAssetClicked(evt, cell));
			assetElement.RegisterDrag(this);
			img.AddManipulator(new ContextualMenuManipulator(evt => AddAssetContextMenu(evt, cell.userData as AssetResult)));
			label.AddManipulator(new ContextualMenuManipulator(evt => AddAssetContextMenu(evt, cell.userData as AssetResult)));
			return cell;
		}

		private void BindGridCell(VisualElement cell, AssetResult result)
		{
			UnbindGridCell(cell);
			cell.userData = result;
			cell.style.display = DisplayStyle.Flex;
			var assetElement = cell.Q<LibraryAssetElement>();
			assetElement.Result = result;
			assetElement.SetSize(_thumbnailSize);
			cell.Q<Label>("label").text = result.Asset.Name;
			_visibleElements[result] = cell;
			cell.EnableInClassList("selected", _selectedResults.Contains(result));
			LoadThumb(cell, result.Asset);
		}

		private void UnbindGridCell(VisualElement cell)
		{
			CancelThumbnailLoad(cell);
			if (cell.userData is AssetResult previousResult) {
				_visibleElements.Remove(previousResult);
			}
			cell.userData = null;
			cell.RemoveFromClassList("selected");
			cell.style.display = DisplayStyle.None;
			var assetElement = cell.Q<LibraryAssetElement>();
			if (assetElement != null) {
				assetElement.Result = null;
			}
			var image = cell.Q<Image>("thumbnail");
			if (image != null) {
				image.image = null;
			}
		}

		private void OnGridGeometryChanged(GeometryChangedEvent evt)
		{
			var columnCount = CalculateGridColumnCount(evt.newRect.width);
			if (columnCount == _gridColumnCount) {
				return;
			}
			_gridColumnCount = columnCount;
			RebuildGridRows();
		}

		private void OnGridKeyDown(KeyDownEvent evt)
		{
			if (_assetResults == null || _assetResults.Count == 0) {
				return;
			}
			var currentIndex = _firstSelectedResult == null ? 0 : _assetResults.IndexOf(_firstSelectedResult);
			var targetIndex = evt.keyCode switch {
				KeyCode.LeftArrow => currentIndex - 1,
				KeyCode.RightArrow => currentIndex + 1,
				KeyCode.UpArrow => currentIndex - _gridColumnCount,
				KeyCode.DownArrow => currentIndex + _gridColumnCount,
				KeyCode.Home => 0,
				KeyCode.End => _assetResults.Count - 1,
				_ => currentIndex
			};
			if (targetIndex == currentIndex && evt.keyCode is not KeyCode.Home and not KeyCode.End) {
				return;
			}
			targetIndex = Mathf.Clamp(targetIndex, 0, _assetResults.Count - 1);
			var target = _assetResults[targetIndex];
			if (evt.shiftKey && _firstSelectedResult != null) {
				SelectRange(_assetResults.IndexOf(_firstSelectedResult), targetIndex);
				LastSelectedResult = target;
			} else {
				SelectOnly(target);
			}
			_gridContent.ScrollToItem(targetIndex / _gridColumnCount);
			evt.StopPropagation();
		}

		private int CalculateGridColumnCount(float width) => Mathf.Max(1, Mathf.FloorToInt((width - 20f) / (_thumbnailSize + 20f)));

		private void UpdateGridMetrics()
		{
			_gridContent.fixedItemHeight = _thumbnailSize + 36f;
			_gridColumnCount = CalculateGridColumnCount(_gridContent.contentRect.width);
		}

		private void RebuildGridRows(bool resetScroll = false)
		{
			if (_gridContent == null || _assetResults == null) {
				return;
			}
			UpdateGridMetrics();
			_gridRowStarts.Clear();
			for (var i = 0; i < _assetResults.Count; i += _gridColumnCount) {
				_gridRowStarts.Add(i);
			}
			_visibleElements.Clear();
			_gridContent.Rebuild();
			if (resetScroll && _gridRowStarts.Count > 0) {
				_gridContent.ScrollToItem(0);
			}
		}

		private async void LoadThumb(VisualElement element, Asset asset)
		{
			CancelThumbnailLoad(element);
			var imageElement = element.Q<Image>("thumbnail");
			imageElement.image = null;
			if (!asset.HasThumbnail) {
				return;
			}

			var key = new ThumbnailKey(asset.GUID, _thumbnailSize);
			if (TryGetCachedThumbnail(key, out var cachedTexture)) {
				SetThumbnail(imageElement, cachedTexture);
				return;
			}

			var thumbnailPath = asset.ThumbnailPath;
			var result = element.userData as AssetResult;
			var cancellation = new CancellationTokenSource();
			var cancellationToken = cancellation.Token;
			_thumbnailLoads[element] = cancellation;
			try {
				await _thumbnailDecodeSlots.WaitAsync(cancellationToken);
				ThumbnailData data;
				try {
					data = await Task.Run(() => {
						cancellationToken.ThrowIfCancellationRequested();
						var decoded = Asset.DecodeThumbnail(thumbnailPath, key.Size);
						cancellationToken.ThrowIfCancellationRequested();
						return decoded;
					}, cancellationToken);
				} finally {
					_thumbnailDecodeSlots.Release();
				}
				await Awaitable.MainThreadAsync();
				cancellationToken.ThrowIfCancellationRequested();
				if (_isDestroyed || element.userData as AssetResult != result) {
					return;
				}
				if (TryGetCachedThumbnail(key, out cachedTexture)) {
					SetThumbnail(imageElement, cachedTexture);
					return;
				}

				var texture = new Texture2D(data.Width, data.Height, TextureFormat.RGB24, false) {
					hideFlags = HideFlags.HideAndDontSave
				};
				texture.LoadRawTextureData(data.Pixels);
				texture.Apply(false, true);
				CacheThumbnail(key, texture, data.Pixels.LongLength);
				SetThumbnail(imageElement, texture);
			} catch (OperationCanceledException) {
				// A recycled cell no longer needs this thumbnail.
			} catch (Exception exception) {
				await Awaitable.MainThreadAsync();
				if (!_isDestroyed) {
					Debug.LogWarning($"Could not load thumbnail {thumbnailPath}: {exception.Message}");
				}
			} finally {
				await Awaitable.MainThreadAsync();
				if (_thumbnailLoads.TryGetValue(element, out var activeLoad) && activeLoad == cancellation) {
					_thumbnailLoads.Remove(element);
				}
				cancellation.Dispose();
			}
		}

		private static void SetThumbnail(Image image, Texture2D texture)
		{
			image.image = texture;
			image.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
			image.style.height = new StyleLength(new Length(100, LengthUnit.Percent));
		}

		private void CancelThumbnailLoad(VisualElement element)
		{
			if (!_thumbnailLoads.TryGetValue(element, out var load)) {
				return;
			}
			_thumbnailLoads.Remove(element);
			load.Cancel();
		}

		private bool TryGetCachedThumbnail(ThumbnailKey key, out Texture2D texture)
		{
			if (!_thumbCache.TryGetValue(key, out var cached)) {
				texture = null;
				return false;
			}
			_thumbLru.Remove(cached.LruNode);
			_thumbLru.AddFirst(cached.LruNode);
			texture = cached.Texture;
			return true;
		}

		private void CacheThumbnail(ThumbnailKey key, Texture2D texture, long sizeBytes)
		{
			if (_thumbCache.TryGetValue(key, out var existing)) {
				_thumbCacheBytes -= existing.SizeBytes;
				_thumbLru.Remove(existing.LruNode);
				DestroyImmediate(existing.Texture);
			}
			var node = _thumbLru.AddFirst(key);
			_thumbCache[key] = new CachedThumbnail(texture, sizeBytes, node);
			_thumbCacheBytes += sizeBytes;
			while (_thumbCacheBytes > ThumbCacheBudget && _thumbLru.Last != null) {
				RemoveCachedThumbnail(_thumbLru.Last.Value);
			}
		}

		private void RemoveCachedThumbnail(ThumbnailKey key)
		{
			if (!_thumbCache.TryGetValue(key, out var cached)) {
				return;
			}
			_thumbCache.Remove(key);
			_thumbLru.Remove(cached.LruNode);
			_thumbCacheBytes -= cached.SizeBytes;
			DestroyImmediate(cached.Texture);
		}

		private void RemoveCachedThumbnails(string guid)
		{
			foreach (var key in _thumbCache.Keys.Where(key => key.Guid == guid).ToArray()) {
				RemoveCachedThumbnail(key);
			}
		}

		private readonly struct ThumbnailKey : System.IEquatable<ThumbnailKey>
		{
			public readonly string Guid;
			public readonly int Size;

			public ThumbnailKey(string guid, int size)
			{
				Guid = guid;
				Size = size;
			}

			public bool Equals(ThumbnailKey other) => Guid == other.Guid && Size == other.Size;
			public override bool Equals(object obj) => obj is ThumbnailKey other && Equals(other);
			public override int GetHashCode() => System.HashCode.Combine(Guid, Size);
		}

		private sealed class CachedThumbnail
		{
			public readonly Texture2D Texture;
			public readonly long SizeBytes;
			public readonly LinkedListNode<ThumbnailKey> LruNode;

			public CachedThumbnail(Texture2D texture, long sizeBytes, LinkedListNode<ThumbnailKey> lruNode)
			{
				Texture = texture;
				SizeBytes = sizeBytes;
				LruNode = lruNode;
			}
		}

		private VisualElement NewAssetLibrary(AssetLibrary lib)
		{
			var item = new VisualElement();
			var toggle = new Toggle();
			var label = new Label(lib.Name);

			item.AddToClassList("library-item");
			item.style.flexDirection = FlexDirection.Row;
			item.Add(toggle);
			item.Add(label);
			var icon = new Image {
				image = lib.IsLocked ? Icons.Locked(IconSize.Small) : Icons.Unlocked(IconSize.Small)
			};
			icon.RegisterCallback<MouseDownEvent>(evt => OnLibraryLockClicked(evt, lib, icon));

			item.Add(icon);

			toggle.value = lib.IsActive;
			toggle.RegisterValueChangedCallback(evt => OnLibraryToggled(lib, evt.newValue));
			label.RegisterCallback<MouseDownEvent>(evt => OnLibraryLabelClicked(evt, lib, toggle, icon));

			item.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
			item.RegisterCallback<DragPerformEvent>(OnDragPerform);
			item.RegisterCallback<DragEnterEvent>(OnDragEnter);
			item.RegisterCallback<DragLeaveEvent>(OnDragLeave);
			item.userData = lib;

			return item;
		}

		private void OnDragPerform(DragPerformEvent evt)
		{
			if (!IsValidDrag(evt.currentTarget)) {
				return;
			}
			if (evt.currentTarget is not VisualElement item) {
				return;
			}
			var lib = item.userData as AssetLibrary;
			if (lib == null) {
				return;
			}
			item.RemoveFromClassList(ClassDrag);

			if (EditorUtility.DisplayDialog("Asset Library", "Are you sure you want to move the assets to this library?", "Yes", "No")) {
				DragAndDrop.AcceptDrag();

				var assets = DragAndDrop.GetGenericData("assets") as HashSet<AssetResult>;
				foreach (var asset in assets) {
					asset.Asset.Library.MoveAsset(asset.Asset, lib);
				}
				AssetDatabase.SaveAssets();
				AssetDatabase.Refresh();
			}
		}

		private static void OnDragUpdated(DragUpdatedEvent evt)
		{
			DragAndDrop.visualMode = IsValidDrag(evt.currentTarget)
				? DragAndDropVisualMode.Move
				: DragAndDropVisualMode.Rejected;
		}

		private static bool IsValidDrag(IEventHandler currentTarget)
		{
			DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
			if (currentTarget is not VisualElement item) {
				return false;
			}
			var lib = item.userData as AssetLibrary;
			if (lib == null || lib.IsLocked) {
				return false;
			}
			if (DragAndDrop.GetGenericData("assets") is not HashSet<AssetResult> assets) {
				return false;
			}

			foreach (var asset in assets) {
				// check for locked libraries
				if (asset.Asset.Library.IsLocked) {
					DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
					return false;
				}

				if (asset.Asset.Library == lib) {
					// don't allow dragging to the same library
					return false;
				}
			}
			return true;
		}

		private void OnDragEnter(DragEnterEvent evt)
		{
			if (!IsValidDrag(evt.currentTarget)) {
				return;
			}
			if (evt.currentTarget is not VisualElement item) {
				return;
			}
			if (IsDraggingExistingAssets) {
				item.AddToClassList(ClassDrag);
			} else {
				return;
			}
		}

		private void OnDragLeave(DragLeaveEvent evt)
		{
			if (evt.currentTarget is not VisualElement item) {
				return;
			}
			item.RemoveFromClassList(ClassDrag);
		}

		private void OnLibraryLabelClicked(IMouseEvent evt, AssetLibrary lib, Toggle toggle, VisualElement icon)
		{
			if (!lib.IsLocked && (evt.ctrlKey || evt.commandKey)) {
				lib.IsLocked = true;
				icon.visible = true;
				_detailsElement.Refresh();
			} else {
				toggle.value = !toggle.value;
			}
		}

		private void OnLibraryLockClicked(IMouseEvent evt, AssetLibrary lib, VisualElement icon)
		{
			if (evt.ctrlKey || evt.commandKey) {
				lib.IsLocked = !lib.IsLocked;
				((Image)icon).image = lib.IsLocked ? Icons.Locked(IconSize.Small) : Icons.Unlocked(IconSize.Small);
				_detailsElement.Refresh();
			}
		}
	}
}
