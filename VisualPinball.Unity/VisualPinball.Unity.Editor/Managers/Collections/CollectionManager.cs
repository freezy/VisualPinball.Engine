using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
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

		[MenuItem("Visual Pinball/Collection Manager", false, 105)]
		public static void ShowWindow()
		{
			GetWindow<CollectionManager>();
		}

		protected override void OnEnable()
		{
			titleContent = new GUIContent("Collection Manager", EditorGUIUtility.IconContent("FolderOpened Icon").image);
			base.OnEnable();
			CheckGUI();
			_availableItems.Reload();
			_collectionItems.Reload();

			ItemInspector.ItemRenamed += OnItemRenamed;
			Undo.undoRedoPerformed += RebuildItemLists;
		}

		protected virtual void OnDisable()
		{
			ItemInspector.ItemRenamed -= OnItemRenamed;
			Undo.undoRedoPerformed -= RebuildItemLists;
		}

		#region Events
		private void OnItemRenamed(IIdentifiableItemAuthoring item, string oldName, string newName)
		{
			//Have to update this name in all Collections
			foreach (var collection in _table.Collections) {
				collection.Data.ItemNames = collection.Data.ItemNames.Select(n => string.Compare(n, oldName, StringComparison.InvariantCultureIgnoreCase) == 0 ? newName : n).ToArray();
			}
			RebuildItemLists();
		}

		private void ItemsToCollection(CollectionTreeElement[] obj)
		{
			AddItemsToCollection();
		}

		private void ItemsToAvailable(CollectionTreeElement[] obj)
		{
			RemoveItemsFromCollection();
		}
		#endregion

		#region GUI
		private void CheckGUI()
		{
			if (_searchAvailable == null) {
				_searchAvailable = new SearchField();
			}
			if (_availableItems == null) {
				_availableItems = new CollectionTreeView();
				_availableItems.ItemDoubleClicked += ItemsToCollection;
			}
			if (_searchCollection == null) {
				_searchCollection = new SearchField();
			}
			if (_collectionItems == null) {
				_collectionItems = new CollectionTreeView();
				_collectionItems.ItemDoubleClicked += ItemsToAvailable;
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

			CheckGUI();
			//rebuild lists
			var rootAvailable = _availableItems.Root;
			rootAvailable.Children.Clear();
			var rootCollection = _collectionItems.Root;
			rootCollection.Children.Clear();

			//Build Collection list in the ItemNames order
			var itemNames = _selectedItem.CollectionData.ItemNames?.Select(n => n) ?? new string[0];
			rootCollection.AddChildren(itemNames.Select(n => new CollectionTreeElement(n)).ToArray());

			//Keep the available items 
			var items = _table.Item.GameItemInterfaces
							.Where(i => !(i is Table) && !string.IsNullOrEmpty(i.Name) && !itemNames.Contains(i.Name))
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

			RecordUndo($"Add {itemNames.Count} item(s) to collection {collectionData.Name}", _selectedItem.CollectionData);

			collectionData.ItemNames = itemNames.Distinct().ToArray();
			_availableItems.SetSelection(new List<int>());
			RebuildItemLists();
		}

		private void RemoveItemsFromCollection()
		{
			var names = _collectionItems.GetSelection().Select(id => _collectionItems.Root.Find(id).Name);
			CollectionData collectionData = _selectedItem.CollectionData;
			var itemNames = collectionData.ItemNames.Except(names).ToArray();

			RecordUndo($"Remove {itemNames.Length} item(s) from collection {collectionData.Name}", _selectedItem.CollectionData);

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
			var selectedItems = _collectionItems.GetSelectedElements().Select(e => e.Name)
								.OrderBy(e => Array.IndexOf(items, e) * (Math.Sign(increment) ? 1 : -1))
								.ToArray();
			string undoName = "Move Item(s) In Collection";
			RecordUndo(undoName, _selectedItem.CollectionData);
			foreach (var item in selectedItems) {
				OffsetSelectedItem(item, increment, selectedItems);
			}
			RebuildItemLists();
			_collectionItems.SetSelectedElements(e => selectedItems.Contains(e.Name));
		}

		private void OffsetSelectedItem(string itemName, int increment, string[] selectedItems)
		{
			var items = _selectedItem.CollectionData.ItemNames;

			for (var i = 0; i < items.Length; ++i) {
				var item = items[i];
				if (item == itemName) {
					var nextIdx = math.clamp(i + increment, 0, items.Length - 1);
					if (nextIdx != i) {
						while (selectedItems.Contains(items[nextIdx])) {
							nextIdx += Math.Sign(increment) ? 1 : -1;
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
				var colData = c.Data;
				data.Add(new CollectionListData { CollectionData = colData });
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
			Undo.RecordObject(_table, undoName);

			var newCol = new Collection(newName);
			_table.Collections.Add(newCol);
			UpdateTableCollections();
		}

		protected override void RemoveData(string undoName, CollectionListData data)
		{
			Undo.RecordObject(_table, undoName);

			_table.Collections.Remove(data.Name);
			UpdateTableCollections();
		}

		protected override int MoveData(string undoName, CollectionListData data, int increment)
		{
			int newIdx = math.clamp(_selectedItem.CollectionData.StorageIndex + increment, 0, _table.Collections.Count - 1);
			_table.Collections.Move(_selectedItem.CollectionData.Name, newIdx);
			UpdateTableCollections();
			return newIdx;
		}

		private void OnDataChanged(string undoName, CollectionData collectionData)
		{
			RecordUndo(undoName, collectionData);
		}

		private void RecordUndo(string undoName, CollectionData collectionData)
		{
			if (_table == null) { return; }

			// Run over table's collection scriptable object wrappers to find the one being edited and add to the undo stack
			foreach (var tableCol in _table.Collections.SerializedObjects) {
				if (tableCol.Data == collectionData) {
					Undo.RecordObject(tableCol, undoName);
					break;
				}
			}
		}

		protected override void RenameExistingItem(CollectionListData data, string newName)
		{
			string oldName = data.CollectionData.Name;

			// give each editable item a chance to update its fields
			string undoName = "Rename Collection";
			RecordUndo(undoName, data.CollectionData);

			data.CollectionData.Name = newName;
		}

		protected override void OnDataSelected()
		{
			RebuildItemLists();
		}
		#endregion

	}
}
