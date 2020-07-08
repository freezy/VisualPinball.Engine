using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace VisualPinball.Unity.Editor.Layers
{

	/// <summary>
	/// This Editor will handle all Layers management as VPX does.
	/// It's using a custom LayersTreeView
	/// </summary>
	public class LayersEditor : EditorWindow
	{
		private TreeViewState _treeViewState;
		private TreeView _treeView;


		[MenuItem("Visual Pinball/Layers Manager", false, 101)]
		public static void ShowWindow()
		{
			GetWindow<LayersEditor>("Layers Manager");
		}

		protected virtual void OnEnable()
		{
			if (_treeViewState == null)
				_treeViewState = new TreeViewState();

			_treeView = new LayersTreeView(_treeViewState);
		}


		void OnGUI()
		{
			DoToolbar();
			DoTreeView();
		}

		void DoTreeView()
		{
			Rect rect = GUILayoutUtility.GetRect(0, 100000, 0, 100000);
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
