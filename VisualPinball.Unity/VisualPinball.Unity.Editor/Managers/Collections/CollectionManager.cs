using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VisualPinball.Engine.VPT;
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

		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		[MenuItem("Visual Pinball/Collection Manager", false, 105)]
		public static void ShowWindow()
		{
			GetWindow<CollectionManager>();
		}

		private void CheckGUI()
		{
			if (_searchAvailable == null) {
				_searchAvailable = new SearchField();
			}
			if (_availableItems == null) {
				_availableItems = new CollectionTreeView();
			}
			if (_searchCollection == null) {
				_searchCollection = new SearchField();
			}
			if (_collectionItems == null) {
				_collectionItems = new CollectionTreeView();
			}
		}

		protected override void OnEnable()
		{
			titleContent = new GUIContent("Collection Manager", EditorGUIUtility.IconContent("FolderOpened Icon").image);
			base.OnEnable();
			CheckGUI();
			_availableItems.Reload();
			_collectionItems.Reload();
		}

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

			var items = _table.Item.GameItemInterfaces
							.Where(i => !(i is Table))
							.Where(i => !string.IsNullOrEmpty(i.Name))
							.OrderBy(i => i.Name);

			foreach (var item in items) {
				if (_selectedItem.CollectionData.ItemNames != null && _selectedItem.CollectionData.ItemNames.Contains(item.Name)) {
					rootCollection.AddChild(new CollectionTreeElement(item.Name) { Id = rootCollection.Children.Count });
				} else {
					rootAvailable.AddChild(new CollectionTreeElement(item.Name) { Id = rootAvailable.Children.Count });
				}
			}

			_availableItems.Reload();
			_collectionItems.Reload();
		}

		protected override void OnItemSelected()
		{
			RebuildItemLists();
		}

		private void AddItemsToCollection()
		{
			var names = _availableItems.GetSelection().Select(id => _availableItems.Root.Find(id).Name);
			var collection = _selectedItem.CollectionData.ItemNames?.ToList() ?? new List<string>();
			collection.AddRange(names);
			_selectedItem.CollectionData.ItemNames = collection.Distinct().ToArray();
			_availableItems.SetSelection(new List<int>());
			RebuildItemLists();
		}

		private void RemoveItemsFromCollection()
		{
			var names = _collectionItems.GetSelection().Select(id => _collectionItems.Root.Find(id).Name);
			var collection = _selectedItem.CollectionData.ItemNames.ToList();
			foreach(var name in names) {
				collection.Remove(name);
			}
			_selectedItem.CollectionData.ItemNames = collection.Distinct().ToArray();
			_collectionItems.SetSelection(new List<int>());
			RebuildItemLists();
		}

		protected override void OnDataDetailGUI()
		{
			ToggleField("Fire Events", ref _selectedItem.CollectionData.FireEvents);
			ToggleField("Group Elements", ref _selectedItem.CollectionData.GroupElements);
			ToggleField("Stop Single Events", ref _selectedItem.CollectionData.StopSingleEvents);

			var optionsRect = GUILayoutUtility.GetLastRect();

			GUILayout.BeginHorizontal();

			GUILayout.BeginVertical();
			GUILayout.Label("Available Items");
			_availableItems.searchString = _searchAvailable.OnGUI(_availableItems.searchString);
			if (GUILayout.Button("Add")) {
				AddItemsToCollection();
			}
			var lastRect = GUILayoutUtility.GetLastRect();
			var listRect = new Rect(lastRect.x, lastRect.y + lastRect.height, optionsRect.width * 0.49f, position.height - lastRect.y - lastRect.height);
			_availableItems.OnGUI(listRect);
			GUILayout.EndVertical();

			GUILayout.BeginVertical();
			GUILayout.Label("Collection Items");
			_collectionItems.searchString = _searchCollection.OnGUI(_collectionItems.searchString);
			if (GUILayout.Button("Remove")) {
				RemoveItemsFromCollection();
			}
			lastRect = GUILayoutUtility.GetLastRect();
			listRect = new Rect(lastRect.x, lastRect.y + lastRect.height, optionsRect.width * 0.49f, position.height - lastRect.y - lastRect.height);
			_collectionItems.OnGUI(listRect);
			GUILayout.EndVertical();

			GUILayout.EndHorizontal();
		}

		protected override void RenameExistingItem(CollectionListData data, string newName)
		{
			_table.Collections.SetNameMapDirty();
			string oldName = data.CollectionData.Name;

			// give each editable item a chance to update its fields
			string undoName = "Rename Collection";
			RecordUndo(undoName, data.CollectionData);

			data.CollectionData.Name = newName;
		}

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

		protected override void AddNewData(string undoName, string newName)
		{
			_table.Collections.SetNameMapDirty();
			Undo.RecordObject(_table, undoName);

			var newCol = new Engine.VPT.Collection.Collection(newName);
			_table.Collections.Add(newCol);
			_table.Item.Data.NumCollections = _table.Collections.Count;
		}

		protected override void RemoveData(string undoName, CollectionListData data)
		{
			_table.Collections.SetNameMapDirty();
			Undo.RecordObject(_table, undoName);

			_table.Collections.Remove(data.Name);
			_table.Item.Data.NumCollections = _table.Collections.Count;
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

	}
}
