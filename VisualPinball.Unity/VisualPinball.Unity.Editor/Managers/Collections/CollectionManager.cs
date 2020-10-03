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
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Editor UI for VPX collections, equivalent to VPX's "Collection Manager" window
	/// </summary>
	public class CollectionManager : ManagerWindow<CollectionListData>
	{
		private SearchField _searchAvailable;
		private CollectionTreeView _availableItems;
		private SearchField _searchCollection;
		private CollectionTreeView _collectionItems;

		protected override string DataTypeName => "Collection";
		protected override float DetailsMaxWidth => 500f;

		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		private class SerializedCollections : ScriptableObject
		{
			public TableAuthoring Table;
			public List<CollectionData> Collections = new List<CollectionData>();
		}
		private SerializedCollections _recordCollections;

		[MenuItem("Visual Pinball/Collection Manager", false, 105)]
		public static void ShowWindow()
		{
			GetWindow<CollectionManager>();
		}

		protected override void OnEnable()
		{
			titleContent = new GUIContent("Collection Manager", EditorGUIUtility.IconContent("FolderOpened Icon").image);
			base.OnEnable();
			InitGUI();
			_availableItems.Reload();
			_collectionItems.Reload();

			ItemInspector.ItemRenamed += OnItemRenamed;
		}

		protected virtual void OnDisable()
		{
			ItemInspector.ItemRenamed -= OnItemRenamed;
			if (_availableItems != null) {
				_availableItems.ItemDoubleClicked -= OnAvailableDoubleClick;
			}
			if (_collectionItems != null) {
				_collectionItems.ItemDoubleClicked -= OnCollectionDoubleClick;
			}
		}

		#region Events
		private void OnItemRenamed(IIdentifiableItemAuthoring item, string oldName, string newName)
		{
			//Have to update this name in all Collections
			foreach (var collection in _table.Collections) {
				collection.ItemNames = collection.ItemNames.Select(n => string.Compare(n, oldName, StringComparison.InvariantCultureIgnoreCase) == 0 ? newName : n).ToArray();
			}
			RebuildItemLists();
		}

		private void OnAvailableDoubleClick(CollectionTreeElement[] obj)
		{
			AddItemsToCollection();
		}

		private void OnCollectionDoubleClick(CollectionTreeElement[] obj)
		{
			RemoveItemsFromCollection();
		}
		#endregion

		#region GUI
		private void InitGUI()
		{
			if (_searchAvailable == null) {
				_searchAvailable = new SearchField();
			}
			if (_availableItems == null) {
				_availableItems = new CollectionTreeView();
				_availableItems.ItemDoubleClicked += OnAvailableDoubleClick;
			}
			if (_searchCollection == null) {
				_searchCollection = new SearchField();
			}
			if (_collectionItems == null) {
				_collectionItems = new CollectionTreeView();
				_collectionItems.ItemDoubleClicked += OnCollectionDoubleClick;
			}
		}

		protected override void OnDataDetailGUI()
		{
			ToggleField("Fire Events", ref _selectedItem.CollectionData.FireEvents);
			ToggleField("Group Elements", ref _selectedItem.CollectionData.GroupElements);
			ToggleField("Stop Single Events", ref _selectedItem.CollectionData.StopSingleEvents);

			var optionsRect = GUILayoutUtility.GetLastRect();

			EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(DetailsMaxWidth));

			EditorGUILayout.BeginVertical(GUILayout.MaxWidth(DetailsMaxWidth * 0.5f));
			GUILayout.Label("Available Items");
			GUI.enabled = _availableItems.Root.HasChildren;
			_availableItems.searchString = _searchAvailable.OnGUI(_availableItems.searchString);
			if (GUILayout.Button("Add")) {
				AddItemsToCollection();
			}
			GUI.enabled = true;
			float listwidth = optionsRect.width * 0.5f + GUI.skin.window.margin.horizontal * 2.0f;
			var lastRect = GUILayoutUtility.GetLastRect();
			var listRect = new Rect(lastRect.x, lastRect.y + lastRect.height, listwidth, position.height - lastRect.y - lastRect.height);
			_availableItems.OnGUI(listRect);
			EditorGUILayout.EndVertical();

			EditorGUILayout.BeginVertical(GUILayout.MaxWidth(DetailsMaxWidth * 0.5f));
			GUILayout.Label("Collection Items");
			var itemsCount = _selectedItem.CollectionData.ItemNames?.Length ?? 0;
			GUI.enabled = itemsCount > 0;
			_collectionItems.searchString = _searchCollection.OnGUI(_collectionItems.searchString);
			if (GUILayout.Button("Remove")) {
				RemoveItemsFromCollection();
			}
			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Top")) {
				OffsetSelectedItems(-itemsCount);
			}
			if (GUILayout.Button("Up")) {
				OffsetSelectedItems(-1);
			}
			if (GUILayout.Button("Down")) {
				OffsetSelectedItems(1);
			}
			if (GUILayout.Button("Bottom")) {
				OffsetSelectedItems(itemsCount);
			}
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();
			lastRect = GUILayoutUtility.GetLastRect();
			listRect = new Rect(lastRect.x, lastRect.y + lastRect.height, listwidth, position.height - lastRect.y - lastRect.height);
			_collectionItems.OnGUI(listRect);
			EditorGUILayout.EndVertical();

			EditorGUILayout.EndHorizontal();
		}
		#endregion

		#region Collection items management
		private void RebuildItemLists()
		{
			if (_selectedItem == null) {
				return;
			}

			InitGUI();
			//rebuild lists
			var rootAvailable = _availableItems.Root;
			rootAvailable.Children.Clear();
			var rootCollection = _collectionItems.Root;
			rootCollection.Children.Clear();

			//Build Collection list in the ItemNames order
			var itemNames = _selectedItem.CollectionData.ItemNames?.Select(n => n) ?? new string[0];
			rootCollection.AddChildren(itemNames.Select(n => new CollectionTreeElement(n)).ToArray());

			//Keep the available items 
			var items = _table.Item.GameItems
							.Where(i => !string.IsNullOrEmpty(i.Name) && !itemNames.Contains(i.Name))
							.OrderBy(i => i.Name);
			rootAvailable.AddChildren(items.Select(i => new CollectionTreeElement(i.Name)).ToArray());

			_availableItems.Reload();
			_collectionItems.Reload();
		}

		private void AddItemsToCollection()
		{
			var names = _availableItems.GetSelection().Select(id => _availableItems.Root.Find(id).Name);
			var collectionData = _selectedItem.CollectionData;
			var itemNames = collectionData.ItemNames?.ToList() ?? new List<string>();
			itemNames.AddRange(names);

			RecordUndo($"Add {itemNames.Count} item(s) to collection {collectionData.Name}");

			collectionData.ItemNames = itemNames.Distinct().ToArray();
			_availableItems.SetSelection(new List<int>());
			RebuildItemLists();
		}

		private void RemoveItemsFromCollection()
		{
			var names = _collectionItems.GetSelection().Select(id => _collectionItems.Root.Find(id).Name);
			CollectionData collectionData = _selectedItem.CollectionData;
			var itemNames = collectionData.ItemNames.Except(names).ToArray();

			RecordUndo($"Remove {itemNames.Length} item(s) from collection {collectionData.Name}");

			_selectedItem.CollectionData.ItemNames = itemNames.Distinct().ToArray();
			_collectionItems.SetSelection(new List<int>());
			RebuildItemLists();
		}

		private void OffsetSelectedItems(int increment)
		{
			if (increment == 0) {
				return;
			}
			var items = _selectedItem.CollectionData.ItemNames;
			//Items are ordered using the increment so we'll treat them in the correct order for stacking if needed
			var selectedItems = _collectionItems.GetSelectedElements().Select(e => e.Name)
								.OrderBy(e => Array.IndexOf(items, e) * (Math.Sign(increment) ? 1 : -1))
								.ToArray();
			RecordUndo($"Move {selectedItems.Length} item(s) In Collection {_selectedItem.CollectionData.Name}");
			foreach (var item in selectedItems) {
				OffsetSelectedItem(item, increment, selectedItems);
			}
			RebuildItemLists();
			_collectionItems.SetSelectedElements(e => selectedItems.Contains(e.Name));
		}

		/// <summary>
		/// Move one collection item by increment value in the collection.
		/// Will ensure that all provided selected elements are not overridden by this one
		/// </summary>
		/// <param name="itemName">The item name to move</param>
		/// <param name="increment">The increment t use to move the item</param>
		/// <param name="selectedItems">The list of other selected items to check overrides</param>
		/// <remarks>
		/// If the current moved item ends up on another item, it will go backward from the increment until it found a free spot where it can switch with an unselected item
		/// </remarks>
		private void OffsetSelectedItem(string itemName, int increment, string[] selectedItems)
		{
			var items = _selectedItem.CollectionData.ItemNames;

			for (var i = 0; i < items.Length; ++i) {
				var item = items[i];
				if (item == itemName) {
					var nextIdx = math.clamp(i + increment, 0, items.Length - 1);
					if (nextIdx != i) {
						//while the new index is already used by a selected item, we go backward the increment 1 by 1 to find a free spot to swap with
						while (selectedItems.Contains(items[nextIdx])) {
							nextIdx += Math.Sign(increment) ? 1 : -1;
							//We went back to the original index of that item, we cannot move it
							if (increment > 0 ? nextIdx <= i : nextIdx >= i) {
								return;
							}
						}
						items[i] = items[nextIdx];
						items[nextIdx] = itemName;
						break;
					}
				}
			}
		}
		#endregion


		#region Data management
		protected override List<CollectionListData> CollectData()
		{
			List<CollectionListData> data = new List<CollectionListData>();

			foreach (var c in _table.Collections) {
				data.Add(new CollectionListData { CollectionData = c });
			}

			return data;
		}

		protected override void OnDataChanged(string undoName, CollectionListData data)
		{
			OnDataChanged(undoName, data.CollectionData);
		}

		/// <summary>
		/// This methods will correctly set all the StorageIndex for collection items, so no need to reset them while saving
		/// </summary>
		private void UpdateTableCollections()
		{
			//rebuild storage indexes
			int idx = 0;
			foreach (var collection in _table.Collections) {
				collection.StorageIndex = idx++;
			}
			_table.Item.Data.NumCollections = _table.Collections.Count;
		}

		protected override void AddNewData(string undoName, string newName)
		{
			RecordUndo(undoName);
			var newCol = new CollectionData(newName);
			_table.Collections.Add(newCol);
			UpdateTableCollections();
		}

		protected override void RemoveData(string undoName, CollectionListData data)
		{
			RecordUndo(undoName);
			_table.Collections.Remove(data.CollectionData);
			UpdateTableCollections();
		}

		protected override void CloneData(string undoName, string newName, CollectionListData data)
		{
			RecordUndo(undoName);
			var newCol = new CollectionData(newName, data.CollectionData);
			_table.Collections.Add(newCol);
			UpdateTableCollections();
		}

		protected override int MoveData(string undoName, CollectionListData data, int increment)
		{
			RecordUndo(undoName);
			var index = _table.Collections.IndexOf(data.CollectionData);
			if (index >= 0) {
				var newIdx = math.clamp(index + increment, 0, _table.Collections.Count - 1);
				if (newIdx != index) {
					_table.Collections.RemoveAt(index);
					_table.Collections.Insert(newIdx, data.CollectionData);
					UpdateTableCollections();
				}
			}
			return _selectedItem.CollectionData.StorageIndex;
		}

		private void OnDataChanged(string undoName, CollectionData collectionData)
		{
			RecordUndo(undoName);
		}

		protected override void RenameExistingItem(CollectionListData data, string newName)
		{
			// give each editable item a chance to update its fields
			RecordUndo($"Rename Collection from {data.CollectionData.Name} to {newName}");

			data.CollectionData.Name = newName;
		}

		protected override void OnDataSelected()
		{
			RebuildItemLists();
		}
		#endregion

		#region Undo Redo
		private void RestoreTableCollections()
		{
			if (_recordCollections == null) { return; }
			if (_table == null) { return; }
			if (_recordCollections.Table == _table) {
				_table.RestoreCollections(_recordCollections.Collections);
			}
		}

		protected override void UndoPerformed()
		{
			RestoreTableCollections();
			base.UndoPerformed();
			RebuildItemLists();
		}

		private void RecordUndo(string undoName)
		{
			if (_table == null) { return; }
			if (_recordCollections == null) {
				_recordCollections = CreateInstance<SerializedCollections>();
			}
			_recordCollections.Table = _table;
			_recordCollections.Collections.Clear();
			_recordCollections.Collections.AddRange(_table?.Collections);
			Undo.RecordObjects( new UnityEngine.Object[] { this, _recordCollections} , undoName);
		}
		#endregion

	}
}
