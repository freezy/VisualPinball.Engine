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

		private Rect ToolbarRect => new Rect(10f, 10f, position.width - 20f, 20f);
		private Rect TreeViewRect => new Rect(10f, 30f, position.width - 20f, position.height - 40f);

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

			_treeView = new LayerTreeView(_layerHandler.TreeModel);
			_treeView.LayerRenamed += _layerHandler.OnLayerRenamed;

			_searchField.downOrUpArrowKeyPressed += _treeView.SetFocusAndEnsureSelectedItem;

			SceneVisibilityManager.visibilityChanged += OnVisibilityChanged;

			// trigger handler update
			OnHierarchyChange();
		}

		private void OnHierarchyChange()
		{
			_layerHandler.OnHierarchyChange(null);
			foreach (var tableBehavior in Resources.FindObjectsOfTypeAll<TableBehavior>()) {
				_layerHandler.OnHierarchyChange(tableBehavior);
			}
			_treeView.Reload();
		}

		private void OnDisable()
		{
			SceneVisibilityManager.visibilityChanged -= OnVisibilityChanged;
		}

		private void OnVisibilityChanged()
		{
			_treeView.Repaint();
		}

		private void OnGUI()
		{
			DrawToolbar();
			DrawTreeView();
		}

		private void DrawToolbar()
		{
			_treeView.searchString = _searchField.OnGUI(ToolbarRect, _treeView.searchString);
		}

		private void DrawTreeView()
		{
			_treeView.OnGUI(TreeViewRect);
		}
	}
}
