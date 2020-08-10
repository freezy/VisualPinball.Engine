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
	/// It does mostly coordination. The main logic is implemented in <see cref="LayerHandler"/>,
	/// and tree view of the layers in <see cref="LayerTreeView"/>.
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

			// reload when the layer handler has rebuilt its tree
			_layerHandler.TreeRebuilt += _treeView.Reload;

			// trigger layer updates when layer was renamed
			_treeView.LayerRenamed += _layerHandler.OnLayerRenamed;

			// select layer items / selected item when layer / item was double-clicked
			_treeView.ItemDoubleClicked += LayerHandler.OnItemDoubleClicked;

			// assign new layer when item was dropped onto a layer
			_treeView.ItemsDropped += _layerHandler.OnItemsDropped;

			// show context menu
			_treeView.ItemContextClicked += OnContextClicked;

			_searchField.downOrUpArrowKeyPressed += _treeView.SetFocusAndEnsureSelectedItem;

			// repaint layer when visibility changes
			SceneVisibilityManager.visibilityChanged += OnVisibilityChanged;

			// reload when undo performed
			Undo.undoRedoPerformed += OnUndoRedoPerformed;

			// trigger handler update on enable
			OnHierarchyChange();
		}

		private void OnDisable()
		{
			SceneVisibilityManager.visibilityChanged -= OnVisibilityChanged;
			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
		}

		private void OnGUI()
		{
			GUILayout.BeginHorizontal(EditorStyles.toolbar);
			if (GUILayout.Button(	new GUIContent(EditorGUIUtility.IconContent("CreateAddNew").image, "Create new layer"), 
									new GUIStyle(GUI.skin.FindStyle("RL FooterButton")) { fixedWidth = EditorStyles.toolbar.fixedHeight, fixedHeight = EditorStyles.toolbar.fixedHeight })) {
				_layerHandler.CreateNewLayer();
			}
			_treeView.searchString = _searchField.OnGUI(_treeView.searchString);
			GUILayout.EndHorizontal();

			_treeView.OnGUI(new Rect(GUI.skin.window.margin.left, 
									 EditorStyles.toolbar.fixedHeight, 
									 position.width - GUI.skin.window.margin.horizontal, 
									 position.height - EditorStyles.toolbar.fixedHeight - GUI.skin.window.margin.vertical));
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
					menu.AddItem(new GUIContent($"Assign {elements.Length} item(s) to/<New Layer>"), false, AssignToLayer, new LayerAssignMenuContext { Elements = elements });

					foreach (var layer in _layerHandler.Layers) {
						menu.AddItem(new GUIContent($"Assign {elements.Length} item(s) to/{layer}"), false, AssignToLayer, new LayerAssignMenuContext { Elements = elements, Layer = layer });
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
			_layerHandler.DeleteLayer(((LayerTreeElement) element).Id);
		}

		private void AssignToLayer(object context)
		{
			if (context is LayerAssignMenuContext assignContext) {
				_layerHandler.AssignToLayer(assignContext.Elements, assignContext.Layer);
			}
		}

		/// <summary>
		/// Called each time something is changed in the scene hierarchy (event GameObjects renaming)
		/// </summary>
		private void OnHierarchyChange()
		{
			_layerHandler.OnHierarchyChange(FindObjectOfType<TableBehavior>());
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

		/// <summary>
		/// This context will pass information for Layer assignment menu items (can only pass an object as userData)
		/// </summary>
		private class LayerAssignMenuContext
		{
			public LayerTreeElement[] Elements;
			public string Layer;
		}

	}
}
