// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Styles used by the view to display elements, elements' names & hover container
	/// </summary>
	public class ThumbnailViewStyles
	{
		public GUIStyle DefaultStyle;
		public GUIStyle SelectedStyle;
		public GUIStyle NameStyle;
		public GUIStyle HoverStyle;
		public bool Inited => DefaultStyle != null && SelectedStyle != null && NameStyle != null && HoverStyle != null;
	}

	/// <summary>
	/// A thumbnail view which can be integrated in any Unity GUI layout
	/// It manages <see cref="ThumbnailElement"/> on a planar view.
	/// It'll use the custom <see cref="ThumbnailElement"/> draw methods to display each element in its own rect.
	/// </summary>
	/// <typeparam name="T">a <see cref="ThumbnailElement"/> generic class</typeparam>
	public abstract class ThumbnailView<T> where T : ThumbnailElement
	{
		/// <summary>
		/// Displays the toolbar (name filter, thumnail size selector, potential custom pre/port toolbar elements).
		/// </summary>
		public bool ShowToolbar = true;

		/// <summary>
		/// Display elements' names under each element.
		/// </summary>
		public bool DisplayNames = true;

		/// <summary>
		/// Set single or multiple elements' selection.
		/// </summary>
		public bool MultiSelection = false;

		public struct RowCollums
		{
			public int Rows;
			public int Collumns;

			public bool Valid => Rows > 0 && Collumns > 0;
		}

		private List<T> _data = new List<T>();
		/// <summary>
		/// Does this view contains data to display.
		/// </summary>
		public bool HasData => _data.Count > 0;

		private Vector2 _scroll;

		private List<T> _selectedItems = new List<T>();
		/// <summary>
		/// List of selected items.
		/// </summary>
		public List<T> SelectedItems => _selectedItems;
		/// <summary>
		/// Selected item (null in case of multi selection).
		/// </summary>
		public T SelectedItem => _selectedItems.Count == 1 ? _selectedItems[0] : null;

		private string _searchFilter = string.Empty;

		protected EThumbnailSize _thumbnailSize = EThumbnailSize.Normal;

		protected ThumbnailViewStyles _commonStyles = new ThumbnailViewStyles();

		private enum Filters
		{
			Name, 
			Label,
			Type
		}

		private Dictionary<Filters, HashSet<string>> _filters = new Dictionary<Filters, HashSet<string>> {
			{Filters.Name , new HashSet<string>()},
			{Filters.Label, new HashSet<string>()},
			{Filters.Type, new HashSet<string>()}
		};

		public ThumbnailView(IEnumerable<T> data)
		{
			SetData(data);
		}

		/// <summary>
		/// Fill the view with elements to display.
		/// </summary>
		/// <param name="data">Elements to be displayed.</param>
		public void SetData(IEnumerable<T> data)
        {
			_data.Clear();
			if (data != null) {
				_data.AddRange(data);
            }
        }

		protected abstract void InitCommonStyles();

		protected virtual void OnGUIToolbarBegin(Rect r) { }
		protected virtual void OnGUIToolbarEnd(Rect r) { }
		protected virtual void OnGUIBegin(Rect r) { }
		protected virtual void OnGUIEnd(Rect r) { }

		protected virtual bool MatchLabelFilter(T item, string labelFilter) => false;
		protected virtual bool MatchTypeFilter(T item, string typeFilter) => false;


		private static int FilterCompare(string f1, string f2)
		{
			var f1filter = f1.StartsWith("l:", StringComparison.InvariantCultureIgnoreCase) || f1.StartsWith("t:", StringComparison.InvariantCultureIgnoreCase);
			var f2filter = f2.StartsWith("l:", StringComparison.InvariantCultureIgnoreCase) || f2.StartsWith("t:", StringComparison.InvariantCultureIgnoreCase);
			if (f1filter != f2filter)
				return f1filter ? 1 : -1;
			return f1.CompareTo(f2);
		}

		private float NameLineHeight => DisplayNames ? _commonStyles.NameStyle.lineHeight : 0.0f;

		private void SetupFilters()
		{
			foreach(var set in _filters.Values) {
				set.Clear();
			}

			var filters = _searchFilter.Split(' ').ToList();
			filters.Sort(FilterCompare);
			foreach (var filter in filters) {
				if (string.IsNullOrEmpty(filter)) continue;
				if (filter.StartsWith("l:", StringComparison.InvariantCultureIgnoreCase)) {
					var labelFilter = filter.Split(':')[1];
					if (!string.IsNullOrEmpty(labelFilter)) {
						_filters[Filters.Label].Add(labelFilter);
					}
				} else if (filter.StartsWith("t:", StringComparison.InvariantCultureIgnoreCase)) {
					var typeFilter = filter.Split(':')[1];
					if (!string.IsNullOrEmpty(typeFilter)) {
						_filters[Filters.Type].Add(typeFilter);
					}
				} else {
					_filters[Filters.Name].Add(filter);
				}
			}
		}

		private List<T> GetFilteredItems()
		{
			List<T> filteredByNames = new List<T>();
			List<T> filteredByLabels = new List<T>();
			List<T> filteredByTypes = new List<T>();
			var filteredItems = _filters[Filters.Label].Select(L => _data.Where(I => MatchLabelFilter(I, L)));
			foreach (var items in filteredItems) {
				filteredByLabels = filteredByLabels.Union(items).ToList();
			}
			filteredItems = _filters[Filters.Type].Select(L => _data.Where(I => MatchTypeFilter(I, L)));
			foreach (var items in filteredItems) {
				filteredByTypes = filteredByTypes.Union(items).ToList();
			}

			if (_filters[Filters.Name].Count > 0) {
				filteredByNames = _data.Where(I => _filters[Filters.Name].Any(F => I.Name.Contains(F, StringComparison.InvariantCultureIgnoreCase))).ToList();
			} else {
				filteredByNames.AddRange(_data);
			}

			if (_filters[Filters.Label].Count > 0)
				filteredByNames = filteredByNames.Intersect(filteredByLabels).ToList();
			if (_filters[Filters.Type].Count > 0)
				filteredByNames = filteredByNames.Intersect(filteredByTypes).ToList();
			return filteredByNames;
		}

		public void OnGUI(Rect rect)
        {
			InitCommonStyles();

			OnGUIBegin(rect);

			EditorGUILayout.BeginVertical(GUILayout.Width(rect.width),GUILayout.Height(rect.height));

			if (ShowToolbar) {
				EditorGUILayout.BeginVertical();

				OnGUIToolbarBegin(rect);

				EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Width(rect.width));
				EditorGUILayout.LabelField("Name Filter", GUILayout.Width(100));
				_searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(rect.width-320));

				var buttonRect = EditorGUILayout.GetControlRect(GUILayout.Width(GUI.skin.label.lineHeight));
				buttonRect.width = GUI.skin.label.lineHeight;
				if ((GUI.Button(buttonRect, GUIContent.none, !string.IsNullOrEmpty(_searchFilter) ? "ToolbarSeachCancelButton" : "ToolbarSeachCancelButtonEmpty") && !string.IsNullOrEmpty(_searchFilter))){
					_searchFilter = string.Empty;
					GUIUtility.keyboardControl = 0;
				}

				SetupFilters();

				EditorGUILayout.LabelField("Thumbnail Size", GUILayout.Width(100));
				_thumbnailSize = (EThumbnailSize)EditorGUILayout.EnumPopup(_thumbnailSize, GUILayout.Width(100));
				EditorGUILayout.EndHorizontal();

				OnGUIToolbarEnd(rect);

				EditorGUILayout.EndVertical();
			}

			var filteredItems = GetFilteredItems(); 

			_selectedItems = filteredItems.Intersect(_selectedItems).ToList();

			if (filteredItems.Count > 0) {
				var dimension = filteredItems[0].CommonDimensions[_thumbnailSize];

				var maxRow = ComputeMaxRows(filteredItems, rect.width - dimension.Offset.x * 2.0f);
				if (maxRow > 0) {
					Rect fullRect = EditorGUILayout.GetControlRect(false, ((dimension.Offset.y + dimension.Height + NameLineHeight) * maxRow) + dimension.Offset.y, GUILayout.Width(rect.width - GUI.skin.box.padding.left));
					Rect viewRect = new Rect(fullRect.x, fullRect.y, fullRect.width + 15, rect.height - fullRect.y);

					_scroll = GUI.BeginScrollView(viewRect, _scroll, fullRect, false, true);
					Rect scrolledViewRect = new Rect(viewRect.position + _scroll, viewRect.size);

					int rowCount = 0;
					float rowWidth = 0.0f;
					for (int i = 0; i < filteredItems.Count; i++) {
						var item = filteredItems[i];
						var style = _selectedItems.Contains(item) ? _commonStyles.SelectedStyle : _commonStyles.DefaultStyle;
						var itemW = item.GetWidth(_thumbnailSize, style);
						if (itemW > fullRect.width)
							break;
						if (rowWidth + itemW + dimension.Offset.x > fullRect.width) {
							rowCount++;
							rowWidth = 0.0f;
						} 
						Rect itemRect = new Rect(fullRect.x + rowWidth + dimension.Offset.x, fullRect.y + rowCount * (dimension.Offset.y + dimension.Height + NameLineHeight) + dimension.Offset.y, itemW, dimension.Height);
						if (itemRect.Overlaps(scrolledViewRect)) {
							item.OnGUI(itemRect, style);
							if (!string.IsNullOrEmpty(item.Name) && DisplayNames) {
								var nameRect = new Rect(itemRect);
								nameRect.y += itemRect.height;
								nameRect.height = NameLineHeight;
								GUI.Label(nameRect, item.Name, _commonStyles.NameStyle);
							}
						}

						HandleEvents(item, itemRect);
						rowWidth += itemW + dimension.Offset.x;
					}

					GUI.EndScrollView();
				}
			}

			EditorGUILayout.EndVertical();

			OnGUIEnd(rect);
		}

		public void HandleEvents(T item, Rect itemRect)
		{
			var evt = Event.current;

			switch(evt.type) {

				case EventType.MouseDown: {
					if (evt.button == 0 && item.Selectable && itemRect.Contains(evt.mousePosition)) {
						if (evt.control && MultiSelection) {
							if (!_selectedItems.Contains(item))
								_selectedItems.Add(item);
							else
								_selectedItems.Remove(item);
						} else {
							_selectedItems.Clear();
							_selectedItems.Add(item);
						}
						evt.Use();
					}
					break;
				}
			}

		}

		protected int ComputeMaxRows(List<T> items, float viewWidth)
		{
			InitCommonStyles();

			if (items.Count == 0 || viewWidth <= 0.0f)
				return 0;

			var rowWidth = 0.0f;
			var rowCount = 1;
			foreach (var item in items) {
				var itemWidth = item.GetWidth(_thumbnailSize, _commonStyles.DefaultStyle);
				if (itemWidth > viewWidth) {
					return 0;
				}
				if (rowWidth + itemWidth > viewWidth) {
					rowCount++;
					rowWidth = itemWidth;
				} else {
					rowWidth += itemWidth;
				}
			}

			return rowCount;
		}

		/// <summary>
		/// Will return the computed view height based on view's elements
		/// </summary>
		/// <param name="viewWidth">The wiew width needed to evaluate how many rows are needed.</param>
		/// <param name="filteredItems">If true, will be computed only with filtered elements base on the toolbar's name filter.</param>
		/// <returns>The height of the view to display all needed elements.</returns>
		public float ComputeViewHeight(float viewWidth, bool filteredItems = true)
		{
			if (_data.Count == 0)
				return 0.0f;
			var items = filteredItems ? GetFilteredItems() : _data;
			if (items.Count == 0)
				return 0.0f;
			var dimension = items[0].CommonDimensions[_thumbnailSize];
			return ComputeMaxRows(items, viewWidth) * (dimension.Height + dimension.Offset.y);
		}
	}
}
