using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace VisualPinball.Unity.Editor.Layers
{

	/// <summary>
	/// This Editor will handle all Layers management as VPX does.
	/// It's using a custom LayersTreeView
	/// </summary>
	public class LayerEditor : EditorWindow
	{
		private TreeViewState _treeViewState;
		private TreeView _treeView;


		[MenuItem("Visual Pinball/Layer Manager", false, 101)]
		public static void ShowWindow()
		{
			GetWindow<LayerEditor>("Layer Manager");
		}

		protected virtual void OnEnable()
		{
			if (_treeViewState == null)
				_treeViewState = new TreeViewState();

			_treeView = new LayerTreeView(_treeViewState);

			SceneVisibilityManager.visibilityChanged += OnVisibilityChanged;
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
			Rect rect = GUILayoutUtility.GetRect(0, System.Int32.MaxValue, 0, System.Int32.MaxValue);
			_treeView.OnGUI(rect);
		}

		void DoToolbar()
		{
			GUILayout.BeginHorizontal(EditorStyles.toolbar);
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}

	}

}
