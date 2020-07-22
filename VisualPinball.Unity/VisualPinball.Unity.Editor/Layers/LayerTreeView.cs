using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.SceneManagement;
using VisualPinball.Unity.Editor.Utils.TreeView;
using VisualPinball.Unity.VPT.Table;
using UnityObject = UnityEngine.Object;

namespace VisualPinball.Unity.Editor.Layers
{
	/// <summary>
	/// This Treeview will populate VPX Layers structure as provided by the LayersHandler
	///
	/// It will tell the LayersHandler any change on the layers structure (add/remove/rename/assignation)
	/// 
	/// </summary>
	/// <remarks>
	/// It a first structure draft mirroring the table structure for now, will be changed to fit the LayersHandler afterward
	/// </remarks>
	internal class LayerTreeView : TreeViewWithTreeModel<LayerTreeElement>
	{
		public LayerTreeView(TreeViewState state, TreeModel<LayerTreeElement> model)
			: base(state, model)
		{
			showBorder = true;
			showAlternatingRowBackgrounds = true;
			customFoldoutYOffset = 3f;
		}

		// Custom GUI
		protected override void RowGUI(RowGUIArgs args)
		{
			Event evt = Event.current;
			extraSpaceBeforeIconAndLabel = 18f;

			// Visibility Toggle
			Rect toggleRect = args.rowRect;
			toggleRect.x += GetContentIndent(args.item);
			toggleRect.width = 16f;

			// Ensure row is selected before using the toggle (usability)
			if (evt.type == EventType.MouseDown && toggleRect.Contains(evt.mousePosition))
				SelectionClick(args.item, false);

			if (args.item is TreeViewItem<LayerTreeElement> treeViewItem) {
				EditorGUI.BeginChangeCheck();
				bool isVisible = EditorGUI.Toggle(toggleRect, treeViewItem.Data.IsVisible);
				if (EditorGUI.EndChangeCheck()) {
					treeViewItem.Data.IsVisible = isVisible;
				}
			}

			// Text
			base.RowGUI(args);
		}

		protected override bool CanStartDrag(CanStartDragArgs args)
		{
			return false;
		}

 
		//Layer renaming 
		public event Action<int, string> layerRenamed = delegate { }; 
 
		protected override bool CanRename(TreeViewItem item) 
		{ 
			if (item is TreeViewItem<LayerTreeElement> treeViewItem) { 
				return treeViewItem.Data?.Type == LayerTreeViewElementType.Layer; 
			} 
			return false; 
		} 
 
		protected override void RenameEnded(RenameEndedArgs args) 
		{ 
			// Set the backend name and reload the tree to reflect the new model 
			if (args.acceptedRename) { 
				layerRenamed(args.itemID, args.newName); 
				Reload(); 
			} 
		} 
 	}
}
