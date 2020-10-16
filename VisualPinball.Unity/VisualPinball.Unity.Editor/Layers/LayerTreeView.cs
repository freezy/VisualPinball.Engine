// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The actual tree view that is rendered in the editor window.
	/// </summary>
	///
	/// <remarks>
	/// Populates the VPX Layers structure as provided by the <see cref="LayerHandler"/>. <p/>
	///
	/// It notifies the LayerHandler of any change in the layers structure, such as addition, removal, renaming,
	/// or toggling.
	/// </remarks>
	internal class LayerTreeView : TreeView<LayerTreeElement>
	{
		/// <summary>
		/// Event emitted when a layer is renamed, used by <see cref="LayerHandler"/>.
		/// </summary>
		public event Action<LayerTreeElement, string> LayerRenamed;

		/// <summary>
		/// Event emitted on items dropping validation, used by <see cref="LayerHandler"/>.
		/// </summary>
		public event Action<LayerTreeElement[], LayerTreeElement> ItemsDropped;

		public LayerTreeView(LayerTreeElement root) : base(new TreeViewState(), root)
		{
			showBorder = true;
			showAlternatingRowBackgrounds = true;
			customFoldoutYOffset = 3f;
		}

		private void ExpandTableItem()
		{
			SetExpanded(Root.GetChildren(e => e.Type == LayerTreeViewElementType.Table)
									.Select(e => e.Id)
									.ToList().First(), true);
		}

		#region Selection
		public void SynchronizeSelection(IList<int> selectIds)
		{
			if (GetSelection().SequenceEqual(selectIds)) {
				return;
			}

			CollapseAll();
			ExpandTableItem();
			var layersIds = Root.GetChildren(e => selectIds.Contains(e.Id))
								.Select(e => e.Parent.Id).ToList();
			foreach( var id in layersIds) {
				SetExpanded(id, true);
			}
			SetSelection(selectIds);
			SetFocusAndEnsureSelectedItem();
		}

		public string GetFirstSelectedLayer(bool includeItems = true)
		{
			var idList = GetSelection();
			var layers = Root.GetChildren(e => e.Type == LayerTreeViewElementType.Layer);
			var selectedLayers = layers.Where(layer => idList.Contains(layer.Id)).ToArray();
			if (selectedLayers.Length > 0) {
				return selectedLayers[0].Name;
			}

			if (includeItems) {
				var selectedItems = Root.GetChildren(e => idList.Contains(e.Id) && e.Type == LayerTreeViewElementType.Item);
				if (selectedItems.Length > 0) {
					if (selectedItems[0].Parent is LayerTreeElement layerElt) {
						return layerElt.Name;
					}
				}
			}

			return layers.Length > 0 ? layers[0].Name : string.Empty;
		}
		#endregion

		#region LayerHandler Events
		internal void TableChanged()
		{
			Reload();
			SetSelection(new List<int>());
			CollapseAll();
			ExpandTableItem();
		}
		internal void LayerCreated(LayerTreeElement layer)
		{
			Reload();
			SetSelection(new List<int>() { layer.Id });
			SetFocusAndEnsureSelectedItem();
		}

		internal void ItemsAssigned(LayerTreeElement layer, LayerTreeElement[] items)
		{
			Reload();
			CollapseAll();
			ExpandTableItem();
			SetExpanded(layer.Id, true);
			SetSelection(items.Select(i => i.Id).ToList());
			SetFocusAndEnsureSelectedItem();
		}

		#endregion

		private static readonly Dictionary<LayerTreeElementVisibility, Texture> VisibilityToIcon = new Dictionary<LayerTreeElementVisibility, Texture>() {
			{ LayerTreeElementVisibility.Hidden, EditorGUIUtility.IconContent("scenevis_hidden_hover").image},
			{ LayerTreeElementVisibility.HiddenMixed, EditorGUIUtility.IconContent("scenevis_hidden-mixed_hover").image},
			{ LayerTreeElementVisibility.VisibleMixed, EditorGUIUtility.IconContent("scenevis_visible-mixed_hover").image},
			{ LayerTreeElementVisibility.Visible, EditorGUIUtility.IconContent("scenevis_visible_hover").image},
		};

		// Custom GUI
		protected override void RowGUI(RowGUIArgs args)
		{
			Event evt = Event.current;
			extraSpaceBeforeIconAndLabel = 18f;

			// Visibility Toggle
			Rect toggleRect = args.rowRect;
			toggleRect.x += GetContentIndent(args.item) + 4f;
			toggleRect.width = 16f;

			// Ensure row is selected before using the toggle (usability)
			if (evt.type == EventType.MouseDown && toggleRect.Contains(evt.mousePosition))
				SelectionClick(args.item, false);

			if (args.item is TreeViewItem<LayerTreeElement> treeViewItem) {
				EditorGUI.BeginChangeCheck();
				GUIContent guiC = new GUIContent();
				guiC.image = VisibilityToIcon[treeViewItem.Data.Visibility];
				if (GUI.Button(toggleRect, guiC, GUIStyle.none)) {
					treeViewItem.Data.IsVisible = !treeViewItem.Data.IsVisible;
				}

				// Text
				Rect textRect = args.rowRect;
				textRect.x = toggleRect.xMax + 8f;
				guiC.text = treeViewItem.displayName;
				guiC.image = treeViewItem.icon;
				EditorGUI.LabelField(textRect, guiC);
			}
		}

		#region Search
		protected override bool ValidateSearch(string search, LayerTreeElement element) => element.Type == LayerTreeViewElementType.Item && base.ValidateSearch(search, element);

		#endregion

		#region Rename

		/// <summary>
		/// Only allows layers to be renamed
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		protected override bool ValidateRename(TreeViewItem<LayerTreeElement> item) => item.Data?.Type == LayerTreeViewElementType.Layer;

		protected override void RenameEnded(RenameEndedArgs args)
		{
			// Set the backend name and reload the tree to reflect the new model
			if (args.acceptedRename) {
				var layerElement = Root.Find(args.itemID);
				if (layerElement != null && layerElement.Type == LayerTreeViewElementType.Layer) {
					LayerRenamed?.Invoke(layerElement, args.newName);
					Reload();
				}
			}
		}

		internal void OnItemRenamed(IIdentifiableItemAuthoring item, string oldName, string newName)
		{
			Reload();
		}

		#endregion

		#region Drag and Drop

		protected override bool ValidateStartDrag(LayerTreeElement[] elements)
		{
			if (elements.Length == 0) {
				return false;
			}
			if (elements.Any(e => e.Type != LayerTreeViewElementType.Item)) {
				return false;
			}
			return true;
		}

		protected override DragAndDropVisualMode HandleElementsDragAndDrop(DragAndDropArgs args, LayerTreeElement[] elements)
		{
			if (args.performDrop) {
				if (args.dragAndDropPosition == DragAndDropPosition.UponItem) {
					if (args.parentItem is TreeViewItem<LayerTreeElement> layerTreeItem) {
						ItemsDropped?.Invoke(elements, layerTreeItem.Data);
					}
				}
				Reload();
				DragAndDrop.AcceptDrag();
				return DragAndDropVisualMode.None;
			}

			switch (args.dragAndDropPosition) {
				default: {
					return DragAndDropVisualMode.None;
				}
				case DragAndDropPosition.BetweenItems:
				case DragAndDropPosition.OutsideItems: {
					return DragAndDropVisualMode.Rejected;
				}
				case DragAndDropPosition.UponItem: {
					if (args.parentItem is TreeViewItem<LayerTreeElement> layerTreeItem) {
						return (layerTreeItem.Data.Type == LayerTreeViewElementType.Layer ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected);
					}
					return DragAndDropVisualMode.Move;
				}
			}
		}

		#endregion

	}
}
