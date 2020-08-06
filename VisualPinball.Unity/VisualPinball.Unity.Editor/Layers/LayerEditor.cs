using System;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Layers
{
	/// <summary>
	/// The VPX layer manager window. <p/>
	///
	/// Apart from drawing the UI, it does the following: <ul>
	///   <li>Dispatch changes in the hierarchy to the handler </li>
	///   <li>Dispatch layer renaming to the handler </li>
	///   <li>Dispatch search text to the handler </li>
	///   <li>Trigger repaint if visibility changed in the hierarchy </li></ul>
	///
	/// </summary>
	public class LayerEditor : EditorWindow
	{
		/// <summary>
		/// The search box control. <p/>
		///
		/// Forwards entered text to the tree view, which does its magic automatically and filters.
		/// </summary>
		private SearchField _searchField;

		/// <summary>
		/// Our extended TreeView control
		/// </summary>
		private LayerTreeView _treeView;

		/// <summary>
		/// The handler, which bridges the table data with the tree view data.
		/// </summary>
		private LayerHandler _layerHandler;

		/// <summary>
		/// Rects used for Editor UI partitionning
		/// </summary>
		/// <remarks>
		/// These properties are re-evaluated each time because window width/height could change.
		/// </remarks>
		private Rect SearchRect => new Rect(10f, 10f, position.width - 20f, 20f);
		private Rect TreeViewRect => new Rect(10f, SearchRect.max.y, position.width - 20f, position.height - 40f);

		#region Editor Window creation
		[MenuItem("Visual Pinball/Layer Manager", false, 101)]
		public static void ShowWindow()
		{
			GetWindow<LayerEditor>("Layer Manager");
		}

		private void OnEnable()
		{
			if (_layerHandler== null) {
				_layerHandler = new LayerHandler();
			}

			if (_searchField == null) {
				_searchField = new SearchField();
			}

			_treeView = new LayerTreeView(_layerHandler.TreeRoot);

			_layerHandler.TreeRebuilt += _treeView.OnTreeRebuilt;	// treeview will Reload when the layerHandler has rebuilt its Tree of LayerTreeElements
			
			_treeView.LayerRenamed += _layerHandler.OnLayerRenamed; // LayerHandler will be notified when a renaming process is finished in the TreeView
			_treeView.ItemDoubleClicked += _layerHandler.OnItemDoubleClicked; // LayerHandler will be notified for each TreeViewItem double-click
			_treeView.ItemsDropped += _layerHandler.OnItemsDropped; // LayerHandler will be notified when items are dropped after a DragDrop process

			_treeView.ItemContextClicked += OnContextClicked; // LayerEditor will be notified of any right-click within the TreeView region to open a context menu

			_searchField.downOrUpArrowKeyPressed += _treeView.SetFocusAndEnsureSelectedItem;

			SceneVisibilityManager.visibilityChanged += OnVisibilityChanged; // LayerEditor will be notified of any visibility change from the SceneManagement view to update the LayerHandler

			Undo.undoRedoPerformed += OnUndoRedoPerformed; // LayerEditor will ask LayerHandler to rebuild its layers structure after performing an Undo/Redo

			// trigger handler update on enable
			OnHierarchyChange();
		}

		private void OnDisable()
		{
			SceneVisibilityManager.visibilityChanged -= OnVisibilityChanged;
			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
		}
		#endregion



		#region MenuItems callbacks & helpers
		/// <summary>
		/// This context will pass information for Layer assignment menu items (can only pass an object as userData)
		/// </summary>
		private class LayerAssignMenuContext
		{
			public LayerTreeElement[] Elements;
			public string Layer;
		}

		/// <summary>
		/// Opens a popup menu when right-clicking somewhere in the TreeView
		/// </summary>
		/// <param name="elements">the current selection in the TreeView</param>
		private void OnContextClicked(LayerTreeElement[] elements)
		{
			GenericMenu menu = new GenericMenu();

			menu.AddItem(new GUIContent("<New Layer>"), false, CreateNewLayer);
			if (elements.Length == 1 && elements[0].Type == LayerTreeViewElementType.Layer) {
				menu.AddItem(new GUIContent($"Delete Layer : {elements[0].Name}"), false, DeleteLayer, elements[0]);
			} else {
				menu.AddDisabledItem(new GUIContent($"Delete Layer"), false);
			}

			if (elements.Length > 0) {
				bool onlyItems = elements.Count(e => e.Type != LayerTreeViewElementType.Item) == 0;
				menu.AddSeparator("");
				if (onlyItems) {
					menu.AddItem(new GUIContent($"Assign {elements.Length} item(s) to/<New Layer>"), false, AssignToNewLayer, new LayerAssignMenuContext() { Elements = elements });

					foreach (var layer in _layerHandler.Layers) {
						menu.AddItem(new GUIContent($"Assign {elements.Length} item(s) to/{layer}"), false, AssignToNewLayer, new LayerAssignMenuContext() { Elements = elements, Layer = layer });
					}
				} else {
					menu.AddDisabledItem(new GUIContent("Select only game items to enable layer assignment"));
				}
			}

			menu.ShowAsContext();
		}

		private void CreateNewLayer()
		{
			_layerHandler.CreateNewLayer();
		}

		private void DeleteLayer(object element)
		{
			_layerHandler.DeleteLayer((element as LayerTreeElement).Id);
		}

		private void AssignToNewLayer(object context)
		{
			LayerAssignMenuContext assignContext = context as LayerAssignMenuContext;
			if (assignContext != null) {
				_layerHandler.AssignToLayer(assignContext.Elements, assignContext.Layer);
			}
		}

		#endregion

		#region Unity Scene management callbacks
		/// <summary>
		/// Callled each time something is changed in the SceneView hierarchy (event GameObjects renaming)
		/// </summary>
		private void OnHierarchyChange()
		{
			_layerHandler.OnHierarchyChange(UnityEngine.Object.FindObjectOfType<TableBehavior>());
			_treeView.Reload();
		}

		/// <summary>
		/// Called when the visibility has changed due to <see cref="SceneVisibilityManager"/> operation.
		/// </summary>
		private void OnVisibilityChanged()
		{
			_treeView.Repaint();
		}

		/// <summary>
		/// Called when <see cref="Undo"/> operations are finished after Undo/Redo
		/// </summary>
		private void OnUndoRedoPerformed()
		{
			OnHierarchyChange();
		}
		#endregion

		#region Editor UI
		private void OnGUI()
		{
			_treeView.searchString = _searchField.OnGUI(SearchRect, _treeView.searchString);

			_treeView.OnGUI(TreeViewRect);
		}
		#endregion
	}
}
