using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VisualPinball.Unity.Editor.Utils.Dialogs;

namespace VisualPinball.Unity.Editor
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

		/// <summary>
		/// Will synchronize the selection with changes from global Selection
		/// </summary>
		private bool _synchronizeSelection = false;

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

			// Notify the tree That the table has changed (mainly to reset the state)
			_layerHandler.TableChanged += _treeView.TableChanged;

			// reload when the layer handler has rebuilt its tree
			_layerHandler.TreeRebuilt += _treeView.Reload;

			// notify the treeview to set selection on the new layer
			_layerHandler.LayerCreated += _treeView.LayerCreated;

			// notify the treeview to set focus on the layer where the items are assigned and to set seleiton on items
			_layerHandler.ItemsAssigned += _treeView.ItemsAssigned;

			// trigger layer updates when layer was renamed
			_treeView.LayerRenamed += _layerHandler.OnLayerRenamed;

			// select layer items / selected item when layer / item was double-clicked
			_treeView.ItemDoubleClicked += LayerHandler.OnItemDoubleClicked;

			// assign new layer when item was dropped onto a layer
			_treeView.ItemsDropped += _layerHandler.OnItemsDropped;

			// show context menu
			_treeView.ItemContextClicked += OnContextClicked;

			_searchField.downOrUpArrowKeyPressed += _treeView.SetFocusAndEnsureSelectedItem;

			ItemInspector.ItemRenamed += _treeView.OnItemRenamed;

			// repaint layer when visibility changes
			SceneVisibilityManager.visibilityChanged += OnVisibilityChanged;

			// reload when undo performed
			Undo.undoRedoPerformed += OnUndoRedoPerformed;

			// catch ToolBoxEditor item creation event to assign to the currently selected layer
			ToolboxEditor.ItemCreated += ToolBoxItemCreated;

			// will notify the TreeView if Synchronize Selection is set
			Selection.selectionChanged += SelectionChanged;

			// trigger handler update on enable
			OnHierarchyChange();
		}

		private void ToolBoxItemCreated(GameObject obj)
		{
			var layerName = _treeView.GetFirstSelectedLayer();
			_layerHandler.AssignToLayer(obj, layerName);
		}

		private void OnDisable()
		{
			ItemInspector.ItemRenamed -= _treeView.OnItemRenamed;
			SceneVisibilityManager.visibilityChanged -= OnVisibilityChanged;
			Undo.undoRedoPerformed -= OnUndoRedoPerformed;
			ToolboxEditor.ItemCreated -= ToolBoxItemCreated;
			Selection.selectionChanged -= SelectionChanged;
		}

		private void OnGUI()
		{
			GUILayout.BeginHorizontal(EditorStyles.toolbar);
			if (GUILayout.Button(	new GUIContent(EditorGUIUtility.IconContent("CreateAddNew").image, "Create new layer"), 
									new GUIStyle(GUI.skin.FindStyle("RL FooterButton")) { fixedWidth = EditorStyles.toolbar.fixedHeight, fixedHeight = EditorStyles.toolbar.fixedHeight })) {
				CreateNewLayerWithValidation(Event.current.mousePosition);
				GUIUtility.ExitGUI();
			}
			_treeView.searchString = _searchField.OnGUI(_treeView.searchString);
			GUILayout.EndHorizontal();

			EditorGUI.BeginChangeCheck();
			_synchronizeSelection = GUILayout.Toggle(_synchronizeSelection, "Sync to hierarchy selection");
			if (EditorGUI.EndChangeCheck()) {
				SelectionChanged();
			}

			_treeView.OnGUI(new Rect(GUI.skin.window.margin.left, 
									 EditorStyles.toolbar.fixedHeight * 2f, 
									 position.width - GUI.skin.window.margin.horizontal, 
									 position.height - EditorStyles.toolbar.fixedHeight * 2f - GUI.skin.window.margin.vertical));
		}

		private void SelectionChanged()
		{
			if (_synchronizeSelection) {
				List<GameObject> gObjs = new List<GameObject>();
				gObjs.Add(Selection.activeGameObject);
				gObjs.AddRange(Selection.objects.Where(o => o is GameObject).Select(o => o as GameObject).ToList());
				_treeView.SynchronizeSelection(gObjs.Select(o => o.GetInstanceID()).Distinct().ToList());
			}
		}

		/// <summary>
		/// Opens a popup menu when right-clicking somewhere in the TreeView
		/// </summary>
		/// <param name="elements">the current selection in the TreeView</param>
		private void OnContextClicked(LayerTreeElement[] elements)
		{
			if (elements.Length > 0) {
				GenericMenu menu = new GenericMenu();

				if (elements.Length == 1) {
					switch (elements[0].Type) {
						case LayerTreeViewElementType.Table: {
							menu.AddItem(new GUIContent("<New Layer>"), false, CreateNewLayer, new LayerMenuContext { MousePosition = Event.current.mousePosition });
							break;
						}

						case LayerTreeViewElementType.Layer: {
							menu.AddItem(new GUIContent($"Delete Layer : {elements[0].Name}"), false, DeleteLayer, new LayerMenuContext { Elements = elements } );
							break;
						}

						default: {
							break;
						}
					}
				}

				bool onlyItems = elements.Count(e => e.Type != LayerTreeViewElementType.Item) == 0;
				if (onlyItems) {
					menu.AddItem(new GUIContent($"Assign {elements.Length} item(s) to/<New Layer>"), false, AssignToLayer, new LayerMenuContext { MousePosition = Event.current.mousePosition, Elements = elements });

					foreach (var layer in _layerHandler.Layers) {
						menu.AddItem(new GUIContent($"Assign {elements.Length} item(s) to/{layer}"), false, AssignToLayer, new LayerMenuContext { MousePosition = Event.current.mousePosition, Elements = elements, Layer = layer });
					}
				}

				if (elements.Length > 0) {
					menu.AddSeparator("");
					menu.AddItem(new GUIContent($"Select {elements.Length} item(s) in Scene Hierarchy"), false, SelectInHierarchy, new LayerMenuContext { MousePosition = Event.current.mousePosition, Elements = elements });
				}

				if (menu.GetItemCount() > 0) {
					menu.ShowAsContext();
					Event.current.Use();
				}
			}

		}

		private void CreateNewLayer(object context)
		{
			if (context is LayerMenuContext createContext) {
				CreateNewLayerWithValidation(createContext.MousePosition);
			}
		}

		private void DeleteLayer(object context)
		{
			if (context is LayerMenuContext deleteContext) {
				_layerHandler.DeleteLayer(deleteContext.Elements[0].Id);
			}
		}

		private void AssignToLayer(object context)
		{
			if (context is LayerMenuContext assignContext) {

				var layerName = assignContext.Layer;
				if (string.IsNullOrEmpty(layerName)) {
					layerName = CreateNewLayerWithValidation(assignContext.MousePosition);
				}

				if (!string.IsNullOrEmpty(layerName)) {
					_layerHandler.AssignToLayer(assignContext.Elements, layerName);
				}
			}
		}

		private void SelectInHierarchy(object context)
		{
			if (context is LayerMenuContext selectContext) {
				LayerHandler.OnItemDoubleClicked(selectContext.Elements);
			}
		}

		private string CreateNewLayerWithValidation(Vector2 mousePosition)
		{
			var dialog = TextInputDialog.Create(titleContent: new GUIContent("Create New Layer"),
												position: new Rect(mousePosition.x, mousePosition.y, 400f, 108f),
												message: "Please enter a valid layer name.\nCannot have several layers with the same name.",
												inputLabel: "Layer Name",
												text: _layerHandler.GetNewLayerValidName(),
												validationDelegate: _layerHandler.ValidateNewLayerName
												);

			dialog.ShowModal();
			if (dialog.TextValidated) {
				_layerHandler.CreateNewLayer(dialog.Text);
				return dialog.Text;
			}

			return null;
		}

		/// <summary>
		/// Called each time something is changed in the scene hierarchy (event GameObjects renaming)
		/// </summary>
		private void OnHierarchyChange()
		{
			_layerHandler.OnHierarchyChange(FindObjectOfType<TableAuthoring>());
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
		/// This context will pass information for Layer creation/deletion/assignment (can only pass an object as userData)
		/// </summary>
		private class LayerMenuContext
		{
			public Vector2 MousePosition;
			public LayerTreeElement[] Elements;
			public string Layer;
		}

	}
}
