using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Layers
{

	/// <summary>
	/// This Editor will handle all Layers management as VPX does.
	/// It's using a custom LayersTreeView
	/// </summary>
	public class LayerEditor : EditorWindow
	{
		private TreeViewState _treeViewState;
		private LayerTreeView _treeView; 
		private SearchField _searchField;

		private LayerHandler _layerHandler;

		private Rect _toolbarRect { get { return new Rect(20f, 10f, position.width - 40f, 20f); } }
		private Rect _treeViewRect { get { return new Rect(20, 30, position.width - 40, position.height - 60); } }

		[MenuItem("Visual Pinball/Layer Manager", false, 101)]
		public static void ShowWindow()
		{
			GetWindow<LayerEditor>("Layer Manager");
		}

		protected virtual void OnEnable()
		{
			if (_treeViewState == null) {
				_treeViewState = new TreeViewState();
			}

			if (_layerHandler== null) {
				_layerHandler = new LayerHandler();
			}

			if (_searchField == null) {
				_searchField = new SearchField();
			}

			_treeView = new LayerTreeView(_treeViewState, _layerHandler.TreeModel);
			_treeView.layerRenamed += _layerHandler.OnLayerRenamed; 
 
			_searchField.downOrUpArrowKeyPressed += _treeView.SetFocusAndEnsureSelectedItem; 

			SceneVisibilityManager.visibilityChanged += OnVisibilityChanged;
			OnHierarchyChange();
		}


		void OnHierarchyChange()
		{
			var all = Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[];
			_layerHandler.OnHierarchyChange(null);
			foreach (var gameObj in all) {
				var tableBehavior = gameObj.GetComponent<TableBehavior>();
				if (tableBehavior != null){
					_layerHandler.OnHierarchyChange(tableBehavior);
					break;
				}
			}
			_treeView.Reload();
		}


		protected virtual void OnDisable()
		{
			SceneVisibilityManager.visibilityChanged -= OnVisibilityChanged;
		}

		private void OnVisibilityChanged()
		{
			_treeView.Repaint();
		}

		void OnGUI()
		{
			DoToolbar();
			DoTreeView();
		}

		void DoTreeView()
		{
			_treeView.OnGUI(_treeViewRect);
		}

		void DoToolbar()
		{
			_treeView.searchString = _searchField.OnGUI(_toolbarRect, _treeView.searchString);
		}

	}

}
