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

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace VisualPinball.Unity.Editor
{
	[InitializeOnLoad]
	public static class EditorTable
	{
		/// <summary>
		/// Returns the most recently active table.
		/// </summary>
		public static TableAuthoring ActiveTable { get; private set; }

		/// <summary>
		/// Returns true if there is an active table component.
		/// </summary>
		public static bool HasActiveTable { get; private set; }

		/// <summary>
		/// Returns a list of current tables.
		/// </summary>
		public static List<TableAuthoring> Tables { get; private set; }

		/// <summary>
		/// Static constructor called on domain reload thanks to [InitializeOnLoad].
		/// </summary>
		static EditorTable()
		{
			Selection.selectionChanged += SetTableFromSelection;
			GetActiveTable();
		}

		/// <summary>
		/// Returns the currently active table if known, otherwise returns the first active table in the scene.
		/// </summary>
		/// <returns>TableAuthoring Reference for the active table.</returns>
		public static TableAuthoring GetActiveTable(bool force = false)
		{
			if (ActiveTable == null || force) {
				FindFirstActiveTable();
			}

			return ActiveTable;
		}

		/// <summary>
		/// Returns a list of all table authoring components.
		/// </summary>
		/// <returns>A TableAuthoring component list of all tables in the scene.</returns>
		public static List<TableAuthoring> GetAllTables()
		{
			FindAllTables();
			if (Tables.Count == 0) {
				FindAllTables(true);
			}
			return Tables;
		}

		public static Bounds GetTableBounds()
		{
			var tableBounds = new Bounds();
			if (HasActiveTable) {
				var mrs = GetActiveTable().GetComponentsInChildren<Renderer>();
				foreach (var mr in mrs) {
					tableBounds.Encapsulate(mr.bounds.max);
					tableBounds.Encapsulate(mr.bounds.min);
					tableBounds.Encapsulate(mr.bounds.center);
				}
			}

			return tableBounds;
		}

		/// <summary>
		/// Callback from selection change to set the active table and add it to the list if it doesn't already exist.
		/// </summary>
		private static void SetTableFromSelection()
		{
			if (Selection.activeGameObject == null) {
				return;
			}

			// check to see if the selection's table is different from the current one being used by this manager
			var selectedTable = Selection.activeGameObject.GetComponentInParent<TableAuthoring>();
			if (selectedTable != null) {

				// Add table to table list.
				if (!Tables.Contains(selectedTable)) {
					Tables.Add(selectedTable);
				}

				SetActiveTable(selectedTable);
			}
		}

		/// <summary>
		/// Returns the table authoring component from the selected child object.
		/// </summary>
		/// <param name="target">Selected child object</param>
		/// <returns>Table component of selected element or null</returns>
		private static TableAuthoring GetTableFromSelection(GameObject target)
		{
			return target != null
				? target.GetComponentInParent<TableAuthoring>()
				: null;
		}

		private static void FindFirstActiveTable()
		{
			FindAllTables();
			if (Tables.Count == 0) FindAllTables(true); //No root level tables were found, search for nested tables.


			foreach (var tableComponent in Tables) {
				if (tableComponent.isActiveAndEnabled && tableComponent.gameObject.activeInHierarchy) {
					SetActiveTable(tableComponent);
					return;
				}
			}

			// No Table component was able to be found.  Set ActiveTable to Null.
			HasActiveTable = false;
			ActiveTable = null;
		}

		/// <summary>
		/// Finds all tables in the scene and populates the table list.
		/// By default, this will only return tables that are top level game objects. Nested tables require a deep search.
		/// </summary>
		/// <param name="deepSearch">Enables a search of every object for a table component.</param>
		private static void FindAllTables(bool deepSearch = false)
		{
			var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
			Tables = new List<TableAuthoring>();

			foreach (var go in rootObjects) {
				var tableComponent = go.GetComponent<TableAuthoring>();
				if (tableComponent) {
					Tables.Add(tableComponent);
				}

				// If deep search is enabled, search all child objects of each root object for table components.
				if (deepSearch) {
					var tableComponents = go.GetComponentsInChildren<TableAuthoring>(true);
					foreach (var tac in tableComponents) {
						Tables.Add(tac);
					}
				}
			}

			if (Tables.Count == 0) {
				HasActiveTable = false;
			}
		}

		/// <summary>
		/// Sets the stored active table from the specified table component.
		/// </summary>
		/// <param name="tableComponent">The table authoring component reference.</param>
		private static void SetActiveTable(TableAuthoring tableComponent)
		{
			if (tableComponent) {
				ActiveTable = tableComponent;
				HasActiveTable = true;
			}
		}
	}
}
