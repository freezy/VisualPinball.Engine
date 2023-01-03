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
using Object = UnityEngine.Object;

namespace VisualPinball.Unity
{
	public class TableSelector
	{
		/// <summary>
		/// Returns the most recently active table.
		/// </summary>
		public TableComponent SelectedTable {
			get => _selectedTable;
			set => SetSelectedTable(value);
		}

		public TableComponent SelectedOrFirstTable
		{
			get {
				if (HasSelectedTable) {
					return _selectedTable;
				}
				var selectedTable = Object.FindObjectOfType<TableComponent>();
				if (selectedTable) {
					SetSelectedTable(selectedTable);
				}
				return selectedTable;
			}
		}

		public void TableUpdated()
		{
			OnTableSelected?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Returns true if there is an active table component.
		/// </summary>
		public bool HasSelectedTable => _selectedTable != null;

		public event EventHandler OnTableSelected;

		private static TableSelector _instance;

		private TableComponent _selectedTable;

		private TableSelector()
		{
		}

		public static TableSelector Instance => _instance ??= new TableSelector();

		private void SetSelectedTable(TableComponent ta)
		{
			_selectedTable = ta;
			OnTableSelected?.Invoke(this, EventArgs.Empty);
		}
	}
}
