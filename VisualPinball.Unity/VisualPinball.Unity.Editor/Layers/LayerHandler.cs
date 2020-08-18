using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Handles user actions within the layer tree. <p/>
	///
	/// It's responsible for: <ul>
	///   <li>Keep the layer tree sync with the game item data </li>
	///   <li>Apply the new layer name to the data when a layer was renamed </li>
	///   <li>Add new layers </li>
	///   <li>Remove layers </li>
	///   <li>Assign a new layer to an item</li>
	///   <li>Select items in the hierarchy on double click</li>
	/// </ul>
	/// </summary>
	///
	/// <remarks>
	/// This class is not aware of the tree *view*, the link between the tree
	/// view and the tree data is the <see cref="TreeRoot"/>.
	/// </remarks>
	internal class LayerHandler
	{
		private const string NewLayerDefaultName = "New Layer ";

		/// <summary>
		/// Expose the list of current layer names (used by <see cref="LayerEditor"/> for populating context menu)
		/// </summary>
		public IEnumerable<string> Layers => _layers.Keys.ToArray();

		/// <summary>
		/// TreeModel used by the <see cref="LayerTreeView"/>, will be built based on the Layers structure
		/// </summary>
		public LayerTreeElement TreeRoot { get; } = new LayerTreeElement { Depth = -1, Id = -1 };

		/// <summary>
		/// this event is fired when the current Table has changed
		/// </summary>
		public event Action TableChanged;

		/// <summary>
		/// this event is fired each time the Tree structure has been updated based on the data gathered from layer BiffData
		/// </summary>
		public event Action TreeRebuilt;

		/// <summary>
		/// Attached <see cref="TableAuthoring"/>, Set by calling OnHierarchyChange
		/// </summary>
		private TableAuthoring _tableAuthoring;

		/// <summary>
		/// Maps the the game items' <see cref="MonoBehaviour"/> to their respective layers.
		/// </summary>
		private Dictionary<string, List<MonoBehaviour>> _layers = new Dictionary<string, List<MonoBehaviour>>();

		/// <summary>
		/// Is called by the <see cref="LayerEditor"/> when a new TableAuthoring is created/deleted
		/// </summary>
		/// <param name="tableAuthoring"></param>
		public void OnHierarchyChange(TableAuthoring tableAuthoring)
		{
			var tableChanged = _tableBehavior != tableBehavior;
			_tableAuthoring = tableAuthoring;
			_layers.Clear();
			Rebuild();
			if (tableChanged) {
				TableChanged?.Invoke();
			}
		}

		#region Construction

		/// <summary>
		/// Recursively runs through the <see cref="TableAuthoring"/>'s children and
		/// adds the game items' <see cref="MonoBehaviour"/> to the layers map. <p/>
		///
		/// It also rebuilds the tree model.
		/// </summary>
		private void Rebuild()
		{
			// keep the empty layers
			_layers = _layers.Where(pair => pair.Value?.Count == 0)
							 .ToDictionary(pair => pair.Key,
										pair => pair.Value);

			// add layers from table data
			if (_tableAuthoring != null) {
				BuildLayersRecursively(_tableAuthoring.gameObject);
			}

			// create tree from table data
			RebuildTree();
		}

		/// <summary>
		/// Recursively runs through the given <see cref="GameObject"/> and
		/// adds its children's <see cref="MonoBehaviour"/> to the layers map.
		/// </summary>
		private void BuildLayersRecursively(GameObject gameObj)
		{
			for (var i = 0; i < gameObj.transform.childCount; ++i) {
				var child = gameObj.transform.GetChild(i).gameObject;
				AddToLayer(child.GetComponent<ILayerableItemAuthoring>());
				BuildLayersRecursively(child);
			}
		}

		private void AddToLayer(ILayerableItemAuthoring item)
		{
			if (item == null) {
				return;
			}
			if (item.EditorLayerName == string.Empty) {
				item.EditorLayerName = $"Layer_{item.EditorLayer + 1}";
			}
			if (!_layers.ContainsKey(item.EditorLayerName)) {
				_layers.Add(item.EditorLayerName, new List<MonoBehaviour>());
			}
			_layers[item.EditorLayerName].Add((MonoBehaviour)item);
		}

		private void RebuildTree()
		{
			TreeRoot.Children.Clear();

			// init with root element
			if (_tableAuthoring != null && _tableAuthoring.Table != null) {

				// table node
				var tableItem = new LayerTreeElement(_tableAuthoring.Table) { Id = 0 };
				TreeRoot.AddChild(tableItem);

				var layerCount = 1;
				foreach (var pair in _layers.OrderBy(key=> key.Key)) {

					// layer node
					var layerItem = new LayerTreeElement(pair.Key) { Id = layerCount++ };
					tableItem.AddChild(layerItem);

					foreach (var item in pair.Value.OrderBy(behaviour => behaviour.name)) {
						if (item is ILayerableItemAuthoring layeredItem) {
							layerItem.AddChild(new LayerTreeElement(layeredItem) { Id = item.gameObject.GetInstanceID() });
						}
					}
				}
			}

			TreeRebuilt?.Invoke();
		}

		#endregion

		#region Rename

		/// <summary>
		/// Applies the new layer name to all items, and updates internals.
		/// </summary>
		/// <param name="element">Renamed layer tree element</param>
		/// <param name="newName">Validated name</param>
		internal void OnLayerRenamed(LayerTreeElement element, string newName)
		{
			if (element.LayerName == newName) {
				return;
			}

			// Check if there is not already a layers with the same name
			if (_layers.ContainsKey(newName)) {
				EditorUtility.DisplayDialog("Visual Pinball", $"There is already a layer named {newName}.\nFind another layer name.", "Close");
				return;
			}

			// Rename in _layers
			if (_layers.TryGetValue(element.LayerName, out var items)) {
				_layers.Remove(element.LayerName);
				_layers[newName] = items;
			}
			element.LayerName = newName;

			// Update layer name for all items within this layer
			if (element.HasChildren) {
				ApplyLayerNameToItems(element.GetChildren<LayerTreeElement>(), newName);
			}
			RebuildTree();
		}


		/// <summary>
		/// Recursively updates <see cref="ItemData.EditorLayerName"/> of all provided elements with a new layer name
		/// </summary>
		/// <param name="elements">Tree layer elements to update</param>
		/// <param name="layerName">New layer name</param>
		private static void ApplyLayerNameToItems(IEnumerable<LayerTreeElement> elements, string layerName)
		{
			foreach (var element in elements) {
				if (element.Item != null) {
					ApplyLayerNameToItem(element.Item, layerName);
					if (element.HasChildren) {
						ApplyLayerNameToItems(element.GetChildren<LayerTreeElement>(), layerName);
					}
				}
			}
		}

		/// <summary>
		/// Updates <see cref="ItemData.EditorLayerName"/>, while managing Undo.
		/// </summary>
		/// <param name="item">Tree layer element to update</param>
		/// <param name="layerName">New layer name</param>
		private static void ApplyLayerNameToItem(ILayerableItemAuthoring item, string layerName)
		{
			if (item.EditorLayerName != layerName) {
				if (item is MonoBehaviour behaviour) {
					Undo.RecordObject(behaviour, $"Item {behaviour.name}: Change layer name from {item.EditorLayerName} to {layerName}");
				}
				item.EditorLayerName = layerName;
			}
		}

		#endregion

		#region Add/Remove

		/// <summary>
		/// Create a new layer with first free name formatted as "New Layer {num}"
		/// </summary>
		public string CreateNewLayer()
		{
			var newLayerNum = 0;
			while (_layers.ContainsKey($"{NewLayerDefaultName}{newLayerNum}")) {
				newLayerNum++;
			}
			string newLayerName = $"{NewLayerDefaultName}{newLayerNum}";
			_layers.Add(newLayerName, new List<MonoBehaviour>());
			RebuildTree();
			return newLayerName;
		}

		/// <summary>
		/// Deletes a layer using a TreeElement ID used within TreeRoot
		/// </summary>
		/// <param name="id">the id of the layer TreeElement</param>
		/// <remarks>
		/// Cannot delete the last layer, table need at least one layer
		/// Will transfer all items from the deleted layer to the first layer of the table
		/// </remarks>
		public void DeleteLayer(int id)
		{
			var layerItem = TreeRoot.Find<LayerTreeElement>(id);
			if (layerItem != null && layerItem.Type == LayerTreeViewElementType.Layer) {

				if (_layers.Keys.Count == 1) {
					EditorUtility.DisplayDialog("Visual Pinball", "Cannot delete all layers.", "Close");
					return;
				}

				// Keep layer's items and put them in the first layer
				var items = layerItem.GetChildren<LayerTreeElement>();
				_layers.Remove(layerItem.LayerName);
				var firstLayer = TreeRoot.GetChildren<LayerTreeElement>(e => e.Type == LayerTreeViewElementType.Layer)[0];
				foreach (var item in items) {
					item.ReParent(firstLayer);
				}
				Rebuild();
			}
		}
		#endregion

		#region Assign

		internal void OnItemsDropped(LayerTreeElement[] droppedElements, LayerTreeElement newParent)
		{
			AssignToLayer(droppedElements, newParent);
		}

		private void AssignToLayer(LayerTreeElement[] elements, LayerTreeElement layer)
		{
			if (layer.Type != LayerTreeViewElementType.Layer) {
				return;
			}

			foreach (var element in elements) {
				if (element.Type == LayerTreeViewElementType.Item) {
					element.ReParent(layer);
				}
			}
			Rebuild();
		}

		internal void AssignToLayer(LayerTreeElement[] elements, string layerName)
		{
			if (string.IsNullOrEmpty(layerName)) {
				layerName = CreateNewLayer();
			}

			var layer = TreeRoot.Find<LayerTreeElement>(e => e.Type == LayerTreeViewElementType.Layer && e.LayerName == layerName);
			AssignToLayer(elements, layer);
		}

		#endregion

		#region Select

		/// <summary>
		/// Callback when a TreeViewItem is double clicked
		/// </summary>
		/// <param name="element">the TreeElement attached to the TreeViewItem</param>
		internal static void OnItemDoubleClicked(LayerTreeElement element)
		{
			switch (element.Type) {
				case LayerTreeViewElementType.Table:
				case LayerTreeViewElementType.Layer: {
					LayerTreeElement[] items = element.GetChildren<LayerTreeElement>(child => child.Type == LayerTreeViewElementType.Item);
					Selection.objects = items.Select(item => EditorUtility.InstanceIDToObject(item.Id)).ToArray();
					break;
				}

				case LayerTreeViewElementType.Item: {
					Selection.activeObject = EditorUtility.InstanceIDToObject(element.Id);
					break;
				}
			}
		}

		#endregion
	}
}
