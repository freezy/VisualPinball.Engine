using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Editor.Utils.TreeViewWithTreeModel;
using VisualPinball.Unity.VPT;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Layers
{
	/// <summary>
	/// This handler will construct a layer structure from the table loaded data and store it into a TreeModel.
	/// It will then be in charge of layers management (add/remove/rename/item assignation) by listening the TreeModel changed event
	/// When TrereModel has changed it will reupdate all BiffData related to layers
	/// </summary>
	class LayerHandler
	{
		/// <summary>
		/// TreeModel used by the LayerTreeView, will be built based on the Layers structure
		/// </summary>
		public TreeModel<LayerTreeElement> TreeModel { get; private set; } = null;

		/// <summary>
		/// Readable version of the layers structure, will be used to build the TreeModel
		/// </summary>
		public Dictionary<string, List<Behaviour>> Layers { get; private set; } = new Dictionary<string, List<Behaviour>>();

		public VisualPinball.Engine.VPT.Table.Table Table { get; private set; } = null;
		
		public LayerHandler()
		{
			//Initializing the TreeModel with Root item only until a table data is set
			List<LayerTreeElement> elementList = new List<LayerTreeElement>();
			elementList.Add(new LayerTreeElement() { Depth = -1, Id = -1 });
			TreeModel = new TreeModel<LayerTreeElement>(elementList);
		}

		/// <summary>
		/// Is called by the LayerEditor when a new TableBehavior is created/deleted
		/// </summary>
		/// <param name="tableBehavior"></param>
		public void RebuildLayers(TableBehavior tableBehavior)
		{
			Table = tableBehavior != null ? tableBehavior.Table : null;
			Layers.Clear();
			if (tableBehavior != null) {
				BuildLayersRecursively(tableBehavior.gameObject);
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
			if (bh is VPT.ILayerableItemBehavior layerableItemBehavior) {
				if (layerableItemBehavior.EditorLayerName == "") {
					layerableItemBehavior.EditorLayerName = $"Layer_{layerableItemBehavior.EditorLayer + 1}";
				}
				if (!Layers.ContainsKey(layerableItemBehavior.EditorLayerName)) {
					Layers.Add(layerableItemBehavior.EditorLayerName, new List<Behaviour>());
				}
				Layers[layerableItemBehavior.EditorLayerName].Add(bh);
			}
		}

		private void RebuildTreeModel()
		{
			List<LayerTreeElement> elementList = new List<LayerTreeElement>();
			elementList.Add(new LayerTreeElement() { Depth = -1, Id = -1 });
			if (Table != null) {
				var tableItem = new LayerTreeElement(Table) { Depth = 0, Id = 0 };
				elementList.Add(tableItem);
				int layercount = 1;
				bool allLayersVisible = true;
				foreach (var layer in Layers.Keys) {
					var layerItem = new LayerTreeElement(layer) { Depth = 1, Id = layercount++ };
					elementList.Add(layerItem);
					bool allItemsVisible = true;
					foreach (var item in Layers[layer]) {
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
	}
}
