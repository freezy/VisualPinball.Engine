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
		/// Readable version of the layers structure, will be used to build the TreeModel
		/// </summary>
		private readonly Dictionary<string, List<Behaviour>> _layers = new Dictionary<string, List<Behaviour>>();

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

		private void RebuildLayers()
		{
			_layers.Clear();
			if (_tableBehavior != null) {
				BuildLayersRecursively(_tableBehavior.gameObject);
			}

			RebuildTreeModel();
		}

		private void BuildLayersRecursively(GameObject gameObj)
		{
			for (int i = 0; i < gameObj.transform.childCount; ++i) {
				var child = gameObj.transform.GetChild(i).gameObject;
				foreach (var bh in child.GetComponents<Behaviour>()) {
					AddToLayer(bh);
				}
				BuildLayersRecursively(child);
			}
		}

		private void AddToLayer(Behaviour bh)
		{
			if (bh is ILayerableItemBehavior layerableItemBehavior) {
				if (layerableItemBehavior.EditorLayerName == "") {
					layerableItemBehavior.EditorLayerName = $"Layer_{layerableItemBehavior.EditorLayer + 1}";
				}
				if (!_layers.ContainsKey(layerableItemBehavior.EditorLayerName)) {
					_layers.Add(layerableItemBehavior.EditorLayerName, new List<Behaviour>());
				}
				_layers[layerableItemBehavior.EditorLayerName].Add(bh);
			}
		}

		private void RebuildTreeModel()
		{
			List<LayerTreeElement> elementList = new List<LayerTreeElement>();
			elementList.Add(new LayerTreeElement() { Depth = -1, Id = -1 });
			if (_tableBehavior != null && _tableBehavior.Table != null) {
				var tableItem = new LayerTreeElement(_tableBehavior.Table) { Depth = 0, Id = 0 };
				elementList.Add(tableItem);
				int layercount = 1;
				bool allLayersVisible = true;
				foreach (var layer in _layers.Keys) {
					var layerItem = new LayerTreeElement(layer) { Depth = 1, Id = layercount++ };
					elementList.Add(layerItem);
					bool allItemsVisible = true;
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
