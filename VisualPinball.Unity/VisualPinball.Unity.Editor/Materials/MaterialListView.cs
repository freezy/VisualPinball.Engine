using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Materials
{
	class MaterialListView : TreeView
	{
		public event Action<List<Engine.VPT.Material>> MaterialSelected;

		private TableBehavior _table;

		public MaterialListView(TreeViewState treeViewState, TableBehavior table, Action<List<Engine.VPT.Material>> materialSelected) : base(treeViewState)
		{
			MaterialSelected += materialSelected;
			_table = table;

			var columns = new[]
			{
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("Name"),
					headerTextAlignment = UnityEngine.TextAlignment.Left,
					canSort = true,
					sortedAscending = true,
					sortingArrowAlignment = UnityEngine.TextAlignment.Right,
					width = 300,
					minWidth = 100,
					maxWidth = float.MaxValue,
					autoResize = true,
					allowToggleVisibility = false,
				},
				new MultiColumnHeaderState.Column
				{
					headerContent = new GUIContent("In use"),
					headerTextAlignment = UnityEngine.TextAlignment.Left,
					canSort = true,
					sortedAscending = true,
					sortingArrowAlignment = UnityEngine.TextAlignment.Right,
					width = 50,
					minWidth = 50,
					maxWidth = 50,
					autoResize = true,
					allowToggleVisibility = false,
				},
			};

			var headerState = new MultiColumnHeaderState(columns);
			this.multiColumnHeader = new MultiColumnHeader(headerState);
			this.multiColumnHeader.SetSorting(0, true);
			this.multiColumnHeader.sortingChanged += SortingChanged;
			this.showAlternatingRowBackgrounds = true;
			this.showBorder = true;

			Reload();
			if (GetRows().Count > 0) {
				SetSelection(new List<int> { 0 }, TreeViewSelectionOptions.FireSelectionChanged);
			}
		}

		public void SelectMaterialWithName(string matName)
		{
			var rows = GetRows();
			foreach (var row in rows) {
				if ((row as RowData).Material.Name.ToLower() == matName.ToLower()) {
					SetSelection(new List<int> { row.id }, TreeViewSelectionOptions.FireSelectionChanged);
					return;
				}
			}
			SetSelection(new List<int>(), TreeViewSelectionOptions.FireSelectionChanged);
		}

		private void SortingChanged(MultiColumnHeader multiColumnHeader)
		{
			Reload();
		}

		public override void OnGUI(Rect rect)
		{
			// if the table went away, force a rebuild to empty out the list
			if (_table == null && GetRows().Count > 0) {
				Reload();
			}
			base.OnGUI(rect);
		}

		protected override TreeViewItem BuildRoot()
		{
			return new TreeViewItem { id = -1, depth = -1, displayName = "Root" };
		}

		protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
		{
			var items = new List<TreeViewItem>();
			if (_table == null) return items;

			// collect list of in use materials
			List<string> inUseMaterials = new List<string>();
			foreach (var renderable in _table.Table.Renderables) {
				var mats = renderable.UsedMaterials;
				if (mats != null) {
					foreach (var mat in mats) {
						if (!string.IsNullOrEmpty(mat)) {
							inUseMaterials.Add(mat);
						}
					}
				}
			}

			// get row data for each material
			for (int i = 0; i < _table.Item.Data.Materials.Length; i++) {
				var mat = _table.Item.Data.Materials[i];
				items.Add(new RowData(i, mat, inUseMaterials.Contains(mat.Name)));
			}

			var sortedColumns = this.multiColumnHeader.state.sortedColumns;
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
						switch ((Column)column) {
							case Column.Name:
								compareResult = a.Material.Name.CompareTo(b.Material.Name);
								break;
							case Column.InUse:
								compareResult = a.InUse.CompareTo(b.InUse);
								break;
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

		protected override void RowGUI(RowGUIArgs args)
		{
			for (int i = 0; i < args.GetNumVisibleColumns(); ++i) {
				CellGUI(args.GetCellRect(i), args.item, args.GetColumn(i));
			}
		}

		private void CellGUI(Rect cellRect, TreeViewItem item, int column)
		{
			CenterRectUsingSingleLineHeight(ref cellRect);
			var rowData = item as RowData;
			switch ((Column)column) {
				case Column.Name:
					GUI.Label(cellRect, rowData.Material.Name);
					break;
				case Column.InUse:
					GUI.Label(cellRect, rowData.InUse ? "X" : "");
					break;
			}
		}

		// not supporting multi select for now
		protected override bool CanMultiSelect(TreeViewItem item) => false;

		protected override void SelectionChanged(IList<int> selectedIds)
		{
			List<Engine.VPT.Material> selectedMats = new List<Engine.VPT.Material>();
			var rows = GetRows();
			foreach (var row in rows) {
				if (selectedIds.Contains(row.id)) {
					selectedMats.Add((row as RowData).Material);
				}
			}
			MaterialSelected?.Invoke(selectedMats);
		}

		private class RowData : TreeViewItem
		{
			public readonly Engine.VPT.Material Material;
			public readonly bool InUse;

			public RowData(int id, Engine.VPT.Material mat, bool inUse) : base(id, 0)
			{
				Material = mat;
				InUse = inUse;
			}
		}

		private enum Column
		{
			Name = 0,
			InUse,

			NUM_COLUMNS
		}
	}
}
