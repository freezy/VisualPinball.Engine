using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Editor.Utils.TreeView;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Layers
{
	/// <summary>
	/// This handler will construct a layer structure from the table loaded data and store it into a TreeModel.
	/// It will then be in charge of layers management (add/remove/rename/item assignation) by listening the TreeModel changed event
	/// When TrereModel has changed it will reupdate all BiffData related to layers
	/// </summary>
	internal class LayerHandler
	{
		private TableBehavior _tableBehavior;

		/// <summary>
		/// Maps the the game items' <see cref="MonoBehaviour"/> to their respective layers.
		/// </summary>
		private readonly Dictionary<string, List<MonoBehaviour>> _layers = new Dictionary<string, List<MonoBehaviour>>();

		/// <summary>
		/// TreeModel used by the LayerTreeView, will be built based on the Layers structure
		/// </summary>
		public LayerTreeElement TreeRoot { get; } = new LayerTreeElement { Depth = -1, Id = -1 };

		public event Action TreeRebuilt;


		public LayerHandler()
		{
		}

		/// <summary>
		/// Is called by the LayerEditor when a new TableBehavior is created/deleted
		/// </summary>
		/// <param name="tableBehavior"></param>
		public void OnHierarchyChange(TableBehavior tableBehavior)
		{
			_tableBehavior = tableBehavior;
			RebuildLayers();
		}

		/// <summary>
		/// Recursively runs through the <see cref="TableBehavior"/>'s children and
		/// adds the game items' <see cref="MonoBehaviour"/> to the layers map. <p/>
		///
		/// It also rebuilds the tree model.
		/// </summary>
		private void RebuildLayers()
		{
			_layers.Clear();
			if (_tableBehavior != null) {
				BuildLayersRecursively(_tableBehavior.gameObject);
			}

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
				AddToLayer(child.GetComponent<ILayerableItemBehavior>());
				BuildLayersRecursively(child);
			}
		}

		private void AddToLayer(ILayerableItemBehavior item)
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
			if (_tableBehavior != null && _tableBehavior.Table != null) {

				// table node
				var tableItem = new LayerTreeElement(_tableBehavior.Table) { Depth = 0, Id = 0 };
				TreeRoot.Children.Add(tableItem);

				var layerCount = 1;
				var allLayersVisible = true;
				foreach (var layer in _layers.Keys) {

					// layer node
					var layerItem = new LayerTreeElement(layer) { Depth = 1, Id = layerCount++ };
					tableItem.Children.Add(layerItem);
					var allItemsVisible = true;

					foreach (var item in _layers[layer]) {
						if (item is ILayerableItemBehavior layeredItem) {
							layerItem.Children.Add(new LayerTreeElement(layeredItem) { Depth = 2, Id = item.gameObject.GetInstanceID() });
							allItemsVisible &= layeredItem.EditorLayerVisibility;
						}
					}
					layerItem.IsVisible = allItemsVisible;
					allLayersVisible &= layerItem.IsVisible;
				}
				tableItem.IsVisible = allLayersVisible;
			}

			TreeRebuilt?.Invoke();
		}

		/// <summary>
		/// Callback when LayerTreeView as validated a layer rename
		/// </summary>
		/// <param name="itemId">the id of the renamed layer TreeElement</param>
		/// <param name="newName">the new validated name</param>
		internal void OnLayerRenamed(int itemId, string newName)
		{
			var layerElement = TreeRoot.Find<LayerTreeElement>(itemId);
			if (layerElement != null && layerElement.Type == LayerTreeViewElementType.Layer) {
				layerElement.LayerName = newName;
				if (layerElement.HasChildren) {
					foreach (var item in layerElement.Children) {
						var iLayerable = ((LayerTreeElement)item).Item;
						if (iLayerable != null){
							iLayerable.EditorLayerName = layerElement.LayerName;
						}
					}
				}
				RebuildLayers();
			}
		}

		/// <summary>
		/// Callback when a TreeViewItem is double clicked
		/// </summary>
		/// <param name="element">the TreeElement attached to the TreeViewItem</param>
		internal void OnItemDoubleClicked(LayerTreeElement element)
		{
			switch (element.Type) {
				case LayerTreeViewElementType.Table:
				case LayerTreeViewElementType.Layer: {
					LayerTreeElement[] items = element.GetChildren<LayerTreeElement>(child => ((LayerTreeElement)child).Type == LayerTreeViewElementType.Item);
					List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
					foreach(var item in items) {
						objects.Add(EditorUtility.InstanceIDToObject(item.Id));
					}
					Selection.objects = objects.ToArray();
					break;
				}

				case LayerTreeViewElementType.Item: {
					Selection.activeObject = EditorUtility.InstanceIDToObject(element.Id);
					break;
				}

				default: {
					break;
				}
			}
		}

	}
}
