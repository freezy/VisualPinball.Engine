// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using System.Reflection;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Wraps Unity's TreeView to make a simplified generic (1-dimensional) list view type control
	/// for use by the manager windows
	/// </summary>
	/// <typeparam name="T">class of type IManagerListData that represents the data being edited</typeparam>
	public class ManagerListView<T> : TreeView where T: class, IManagerListData 
	{
		public event Action<List<T>> ItemSelected;

		private List<T> _data = new List<T>();
		private List<ColumnData> _columns = new List<ColumnData>();
		private Action<T, Rect, int> _itemRenderer;

		public ManagerListView(TreeViewState treeViewState, IEnumerable<T> data, Action<T, Rect, int> itemRenderer, Action<List<T>> itemSelected) : base(treeViewState)
		{
			_itemRenderer = itemRenderer;

			ItemSelected += itemSelected;

			// collect up all column attribute flagged fields and properties, then cache the associated member info
			// and build up our column array for the list view
			foreach (var member in typeof(T).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
				if (member.MemberType == MemberTypes.Field || member.MemberType == MemberTypes.Property) {
					var columnAttr = member.GetCustomAttribute<ManagerListColumnAttribute>();
					if (columnAttr != null) {
						var colState = new MultiColumnHeaderState.Column {
							headerContent = new GUIContent(columnAttr.HeaderName ?? member.Name),
							headerTextAlignment = columnAttr.HeaderAlignment,
							canSort = true,
							sortedAscending = true,
							sortingArrowAlignment = TextAlignment.Right,
							width = columnAttr.Width,
							minWidth = columnAttr.Width * 0.5f,
							maxWidth = float.MaxValue,
							autoResize = true,
							allowToggleVisibility = false,
						};
						_columns.Add(new ColumnData {
							Order = columnAttr.Order,
							State = colState,
							MemberInfo = member,
						});
					}
				}
			}
			_columns.Sort((a, b) => a.Order.CompareTo(b.Order));

			var headerState = new MultiColumnHeaderState(_columns.Select(c => c.State).ToArray());
			multiColumnHeader = new MultiColumnHeader(headerState);
			multiColumnHeader.SetSorting(0, true);
			multiColumnHeader.sortingChanged += SortingChanged;
			showAlternatingRowBackgrounds = true;
			showBorder = true;

			SetData(data);
			if (GetRows().Count > 0) {
				SetSelection(new List<int> { 0 }, TreeViewSelectionOptions.FireSelectionChanged);
			}
		}

		public void SetData(IEnumerable<T> data)
		{
			_data.Clear();
			if (data != null) {
				_data.AddRange(data);
			}
			Reload();
		}

		public void SelectItemWithName(string name)
		{
			var rows = GetRows();
			foreach (var row in rows) {
				if ((row as RowData).Data.Name.ToLower() == name.ToLower()) {
					SetSelection(new List<int> { row.id }, TreeViewSelectionOptions.FireSelectionChanged);
					return;
				}
			}
			SetSelection(new List<int>(), TreeViewSelectionOptions.FireSelectionChanged);
		}

		protected override TreeViewItem BuildRoot()
		{
			return new TreeViewItem { id = -1, depth = -1, displayName = "Root" };
		}

		protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
		{
			var items = new List<TreeViewItem>();

			for (int i = 0; i < _data.Count; i++) {
				items.Add(new RowData(i, _data[i]));
			}

			var sortedColumns = multiColumnHeader.state.sortedColumns;
			if (sortedColumns.Length > 0) {
				items.Sort((baseA, baseB) => {
					var a = baseA as RowData;
					var b = baseB as RowData;
					if (a == null) return 1;
					if (b == null) return -1;
					// sort based on multiple columns
					foreach (var column in sortedColumns) {
						bool ascending = multiColumnHeader.IsSortedAscending(column);
						// flip for descending
						if (!ascending) {
							var tmp = b;
							b = a;
							a = tmp;
						}
						int compareResult = 0;

						var aVal = GetColumnValue(a, column) as IComparable;
						var bVal = GetColumnValue(b, column) as IComparable;
						if (aVal != bVal) {
							if (aVal == null) {
								compareResult = 1;
							} else if (bVal == null) {
								compareResult = -1;
							} else {
								compareResult = aVal.CompareTo(bVal);
							}
						}
						// not equal in this column, then return that
						if (compareResult != 0) {
							return compareResult;
						}
					}
					return a.id.CompareTo(b.id);
				});
			}

			return items;
		}

		// not supporting multi select for now
		protected override bool CanMultiSelect(TreeViewItem item) => false;

		protected override void SelectionChanged(IList<int> selectedIds)
		{
			List<T> selectedData = new List<T>();
			var rows = GetRows();
			foreach (var row in rows) {
				if (selectedIds.Contains(row.id)) {
					selectedData.Add((row as RowData).Data);
				}
			}
			ItemSelected?.Invoke(selectedData);
		}

		protected override void RowGUI(RowGUIArgs args)
		{
			for (int i = 0; i < args.GetNumVisibleColumns(); ++i) {
				var data = (args.item as RowData).Data;

				var cellRect = args.GetCellRect(i);

				CenterRectUsingSingleLineHeight(ref cellRect);

				if (_itemRenderer != null)
				{
					_itemRenderer(data, cellRect, args.GetColumn(i));
				}
				else
				{
					CellGUI(cellRect, args.item, args.GetColumn(i));
				}
			}
		}

		private void CellGUI(Rect cellRect, TreeViewItem item, int column)
		{
			var val = GetColumnValue(item, column);
			if (val != null) {
				if (val is bool bVal) {
					GUI.Label(cellRect, bVal ? "X" : "");
				} else {
					GUI.Label(cellRect, val.ToString());
				}
			}
		}

		// use cached reflection info to get T's instance data for a given column
		private object GetColumnValue(TreeViewItem item, int column)
		{
			if (column < 0 && column >= _columns.Count) {
				return null;
			}

			var memberInfo = _columns[column].MemberInfo;
			object val = null;
			switch (memberInfo) {
				case FieldInfo fi: val = fi.GetValue((item as RowData).Data); break;
				case PropertyInfo pi: val = pi.GetValue((item as RowData).Data); break;
			}
			return val;
		}

		private void SortingChanged(MultiColumnHeader multiColumnHeader)
		{
			Reload();
		}

		private class ColumnData
		{
			public int Order;
			public MultiColumnHeaderState.Column State;
			public MemberInfo MemberInfo;
		}

		private class RowData : TreeViewItem
		{
			public T Data;
			public RowData(int id, T data) : base(id, 0) { Data = data; }
		}
	}
}
