using System.Collections.Generic;
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
		public TreeModel<LayerTreeElement> TreeModel { get; }


		public LayerHandler()
		{
			//Initializing the TreeModel with Root item only until a table data is set
			var elementList = new List<LayerTreeElement> {
				new LayerTreeElement {Depth = -1, Id = -1}
			};
			TreeModel = new TreeModel<LayerTreeElement>(elementList);
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

			RebuildTreeModel();
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

		private void RebuildTreeModel()
		{
			// init with root element
			var elementList = new List<LayerTreeElement> { new LayerTreeElement { Depth = -1, Id = -1 } };

			if (_tableBehavior != null && _tableBehavior.Table != null) {

				// table node
				var tableItem = new LayerTreeElement(_tableBehavior.Table) { Depth = 0, Id = 0 };
				elementList.Add(tableItem);

				var layerCount = 1;
				var allLayersVisible = true;
				foreach (var layer in _layers.Keys) {

					// layer node
					var layerItem = new LayerTreeElement(layer) { Depth = 1, Id = layerCount++ };
					elementList.Add(layerItem);
					var allItemsVisible = true;

					foreach (var item in _layers[layer]) {
						if (item is ILayerableItemBehavior layeredItem) {
							elementList.Add(new LayerTreeElement(layeredItem) { Depth = 2, Id = item.gameObject.GetInstanceID() });
							allItemsVisible &= layeredItem.EditorLayerVisibility;
						}
					}
					layerItem.IsVisible = allItemsVisible;
					allLayersVisible &= layerItem.IsVisible;
				}
				tableItem.IsVisible = allLayersVisible;
			}
			TreeModel.SetData(elementList);
		}

		internal void OnLayerRenamed(int itemId, string newName)
		{
			var layerElement = TreeModel.Find(itemId);
			if (layerElement != null && layerElement.Type == LayerTreeViewElementType.Layer) {
				layerElement.LayerName = newName;
				if (layerElement.HasChildren) {
					foreach (var item in layerElement.Children) {
						var iLayerable = ((LayerTreeElement)item).Item;
						if (iLayerable != null){
							iLayerable.EditorLayerName = newName;
						}
					}
				}
				RebuildLayers();
			}
		}

	}
}
