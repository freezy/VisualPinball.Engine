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

using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// This class updates <see cref="TableSelector"/> based on the selection
	/// in the hierarchy.
	/// </summary>
	[InitializeOnLoad]
	public static class TableSelectorHook
	{
		/// <summary>
		/// Initialization for the table manager.
		///
		/// Gets the first active table on initialization and setups callback for selection.
		/// </summary>
		static TableSelectorHook()
		{
			Selection.selectionChanged += SetTableFromSelection;
			EditorApplication.hierarchyChanged += SetTableFromHierarchy;
		}

		/// <summary>
		/// Callback from selection change to set the active table and add it to the list if it doesn't already exist.
		/// </summary>
		private static void SetTableFromSelection()
		{
			// exit out early if nothing is selected
			if (Selection.activeGameObject == null) {
				return;
			}

			// find parent in hierarchy
			var selectedTable = Selection.activeGameObject.GetComponentInParent<TableAuthoring>();
			if (selectedTable != null) {
				TableSelector.Instance.SelectedTable = selectedTable;
			}
		}

		private static void SetTableFromHierarchy()
		{
			TableSelector.Instance.SelectedTable = FindTableInHierarchy();
		}

		private static TableAuthoring FindTableInHierarchy()
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
