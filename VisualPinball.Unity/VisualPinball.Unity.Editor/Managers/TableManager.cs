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

namespace VisualPinball.Unity.Editor.Utils
{
#if UNITY_EDITOR
	[InitializeOnLoad]
	#endif
	public static class TableManager
    {
		#region Variables
		/// <summary>
		/// Returns the most recently active table. 
		/// </summary>
		public static TableAuthoring activeTable { get; private set; }

		/// <summary>
		/// Returns true if there is an active table component. 
		/// </summary>
		public static bool hasActiveTable { get; private set; }

		/// <summary>
		/// Returns a list of current tables. 
		/// </summary>
		public static List<TableAuthoring> tables { get; private set; }

		#endregion



		#region Editor Initialization

		#if UNITY_EDITOR
		static TableManager()
		{
			//Automatic initialization on load. 
			Initialize();
		}
		#endif

		/// <summary>
		/// Initialization for the table manager.  Gets the first active table on initialization and setups callback for selection.  
		/// </summary>
		static void Initialize()
		{
			Selection.selectionChanged += SetTableFromSelection;
			
			GetActiveTable(); 
			
		}
		
		/// <summary>
		/// Callback from selection change to set the active table and add it to the list if it doesn't already exist. 
		/// </summary>
		static void SetTableFromSelection()
		{
			if(Selection.activeGameObject == null) { return; }

			// check to see if the selection's table is different from the current one being used by this manager
			var selectedTable = Selection.activeGameObject.GetComponentInParent<TableAuthoring>();
			if(selectedTable != null)
			{
				//Add table to table list. 
				if(!tables.Contains(selectedTable)) tables.Add(selectedTable); 
				//Assign active table to selection.
				SetActiveTable(selectedTable);
			}
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Returns the currently active table if known, otherwise returns the first active table in the scene. 
		/// </summary>
		/// <returns>TableAuthoring Reference for the active table.</returns>
		public static TableAuthoring GetActiveTable(bool force = false)
		{
			
			if(activeTable == null || force)
			{
				FindFirstActiveTable();
			}

			return activeTable; 

		}

		/// <summary>
		/// Returns a list of all table authoring components.  
		/// </summary>
		/// <returns>A TableAuthoring component list of all tables in the scene.</returns>
		public static List<TableAuthoring> GetAllTables()
		{
			FindAllTables();
			if(tables.Count == 0) FindAllTables(true);

			return tables; 
		}

		#endregion

		#region Private Methods
		/// <summary>
		/// Returns the table authoring component from the selected child object.  
		/// </summary>
		/// <param name="target">Selected child object</param>
		/// <returns></returns>
		private static TableAuthoring GetTableFromSelection(GameObject target)
		{

			TableAuthoring _tableAuthoring = null;

			if(target)
			{
				_tableAuthoring = target.GetComponentInParent<TableAuthoring>();

			}

			return _tableAuthoring;
		}

		private static void FindFirstActiveTable()
		{

			FindAllTables();
			if(tables.Count == 0) FindAllTables(true);  //No root level tables were found, search for nested tables.  
			

			foreach(TableAuthoring _tableComponent in tables)
			{
				
				if(_tableComponent.isActiveAndEnabled && _tableComponent.gameObject.activeInHierarchy)
				{
					SetActiveTable(_tableComponent);
					return; 
				}
			}

			//No Table component was able to be found.  Set ActiveTable to Null. 
			hasActiveTable = false;
			activeTable = null; 
			
		}

		/// <summary>
		/// Finds all tables in the scene and populates the table list. 
		/// By default, this will only return tables that are top level game objects.  Nested tables require a deep search.  
		/// </summary>
		/// <param name="deepSearch">Enables a search of every object for a table component.</param>
		private static void FindAllTables(bool deepSearch = false)
		{
			GameObject[] _rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
			tables = new List<TableAuthoring>(); 

			foreach(GameObject go in _rootObjects)
			{
				TableAuthoring _tableComponent = go.GetComponent<TableAuthoring>();
				if(_tableComponent)
					tables.Add(_tableComponent);

					//If deep search is enabled, search all child objects of each root object for table components.  
				if(deepSearch)
				{
					TableAuthoring[] _tableComponents = go.GetComponentsInChildren<TableAuthoring>(true);
					foreach(TableAuthoring _tac in _tableComponents)
					{
						tables.Add(_tac);
					}
				}

			}

			if(tables.Count == 0)
			{
				hasActiveTable = false;
			}

		}

		/// <summary>
		/// Sets the stored active table from the specified table component.
		/// </summary>
		/// <param name="_tableComponent">The table authoring component reference.</param>
		private static void SetActiveTable(TableAuthoring _tableComponent)
		{
			if(_tableComponent)
			{
				activeTable = _tableComponent;
				hasActiveTable = true;
			}
		}

		#endregion



	}
}
