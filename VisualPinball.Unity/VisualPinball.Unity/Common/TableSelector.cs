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

using System.Linq;
using UnityEngine.SceneManagement;

namespace VisualPinball.Unity
{
	public class TableSelector
	{
		/// <summary>
		/// Returns the most recently active table.
		/// </summary>
		public TableAuthoring SelectedTable {
			get => _selectedTable ? _selectedTable : FindTableInScene();
			set => _selectedTable = value;
		}

		/// <summary>
		/// Returns true if there is an active table component.
		/// </summary>
		public bool HasSelectedTable => SelectedTable != null;

		private static TableSelector _instance;

		private TableAuthoring _selectedTable;

		private TableSelector()
		{
		}

		public static TableSelector Instance => _instance ?? (_instance = new TableSelector());

		private static TableAuthoring FindTableInScene()
		{
			var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

			// try root objects first
			foreach (var go in rootObjects) {
				var ta = go.GetComponent<TableAuthoring>();
				if (ta != null) {
					return ta;
				}
			}

			// do a deep search
			return rootObjects
				.Select(go => go.GetComponentInChildren<TableAuthoring>(true))
				.FirstOrDefault(ta => ta != null);
		}
	}
}
