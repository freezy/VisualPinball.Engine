using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;


namespace VisualPinball.Unity.Editor.Utils.TreeView
{
	/// <summary>
	/// The TreeWiewWithTreeModel class is a TreeView which will display a provided TreeModel
	/// </summary>
	/// <typeparam name="T">a TreeElement generic class</typeparam>
	internal class TreeViewWithTreeModel<T> : UnityEditor.IMGUI.Controls.TreeView where T : TreeElement
	{
		public event Action TreeChanged;
		public event Action<IList<TreeViewItem>> BeforeDroppingDraggedItems;

		private TreeModel<T> _treeModel;
		private readonly List<TreeViewItem> _rows = new List<TreeViewItem>(100);

		protected TreeViewWithTreeModel(TreeViewState state, TreeModel<T> model) : base(state)
		{
			Init(model);
		}

		private void Init (TreeModel<T> model)
		{
			_treeModel = model;
			_treeModel.modelChanged += ModelChanged;
		}

		private void ModelChanged()
		{
			TreeChanged?.Invoke();
			Reload();
		}

		protected override TreeViewItem BuildRoot()
			=> new TreeViewItem<T>(_treeModel.Root.Id, -1, _treeModel.Root.Name, _treeModel.Root);

		protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
		{
			if (_treeModel.Root == null) {
				throw new InvalidOperationException("Tree model root is null. Did you call SetData()?");
			}

			_rows.Clear();
			if (!string.IsNullOrEmpty(searchString)) {
				Search(_treeModel.Root, searchString, _rows);

			} else {
				if (_treeModel.Root.HasChildren) {
					AddChildrenRecursive(_treeModel.Root, 0, _rows);
				}
			}

			// We still need to setup the child parent information for the rows since this
			// information is used by the TreeView internal logic (navigation, dragging etc)
			SetupParentsAndChildrenFromDepths (root, _rows);

			return _rows;
		}

		private void AddChildrenRecursive(T parent, int depth, ICollection<TreeViewItem> newRows)
		{
			foreach (T child in parent.Children) {
				var item = new TreeViewItem<T>(child.Id, depth, child.Name, child);
				newRows.Add(item);

				if (child.HasChildren) {
					if (IsExpanded(child.Id)) {
						AddChildrenRecursive(child, depth + 1, newRows);
					}
					else {
						item.children = CreateChildListForCollapsedParent();
					}
				}
			}
		}

		void Search(T searchFromThis, string search, List<TreeViewItem> result)
		{
			if (string.IsNullOrEmpty(search)) {
				throw new ArgumentException("Invalid search: cannot be null or empty", "search");
			}

			const int kItemDepth = 0; // tree is flattened when searching

			Stack<T> stack = new Stack<T>();
			foreach (var element in searchFromThis.Children) {
				stack.Push((T) element);
			}

			while (stack.Count > 0)
			{
				T current = stack.Pop();
				// Matches search?
				if (current.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					result.Add(new TreeViewItem<T>(current.Id, kItemDepth, current.Name, current));
				}

				if (current.Children != null && current.Children.Count > 0)
				{
					foreach (var element in current.Children)
					{
						stack.Push((T)element);
					}
				}
			}
			SortSearchResult(result);
		}

		protected void SortSearchResult (List<TreeViewItem> rows)
		{
			rows.Sort ((x,y) => EditorUtility.NaturalCompare (x.displayName, y.displayName)); // sort by displayName by default, can be overriden for multicolumn solutions
		}

		protected override IList<int> GetAncestors (int id)
		{
			return _treeModel.GetAncestors(id);
		}

		protected override IList<int> GetDescendantsThatHaveChildren (int id)
		{
			return _treeModel.GetDescendantsThatHaveChildren(id);
		}


		// Dragging
		//-----------

		const string k_GenericDragID = "GenericDragColumnDragging";

		protected override bool CanStartDrag (CanStartDragArgs args)
		{
			return true;
		}

		protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
		{
			if (hasSearch)
				return;

			DragAndDrop.PrepareStartDrag();
			var draggedRows = GetRows().Where(item => args.draggedItemIDs.Contains(item.id)).ToList();
			DragAndDrop.SetGenericData(k_GenericDragID, draggedRows);
			DragAndDrop.objectReferences = new UnityEngine.Object[] { }; // this IS required for dragging to work
			string title = draggedRows.Count == 1 ? draggedRows[0].displayName : "< Multiple >";
			DragAndDrop.StartDrag (title);
		}

		protected override DragAndDropVisualMode HandleDragAndDrop (DragAndDropArgs args)
		{
			// Check if we can handle the current drag data (could be dragged in from other areas/windows in the editor)
			var draggedRows = DragAndDrop.GetGenericData(k_GenericDragID) as List<TreeViewItem>;
			if (draggedRows == null)
				return DragAndDropVisualMode.None;

			// Parent item is null when dragging outside any tree view items.
			switch (args.dragAndDropPosition)
			{
				case DragAndDropPosition.UponItem:
				case DragAndDropPosition.BetweenItems:
					{
						bool validDrag = ValidDrag(args.parentItem, draggedRows);
						if (args.performDrop && validDrag)
						{
							T parentData = ((TreeViewItem<T>)args.parentItem).Data;
							OnDropDraggedElementsAtIndex(draggedRows, parentData, args.insertAtIndex == -1 ? 0 : args.insertAtIndex);
						}
						return validDrag ? DragAndDropVisualMode.Move : DragAndDropVisualMode.None;
					}

				case DragAndDropPosition.OutsideItems:
					{
						if (args.performDrop)
							OnDropDraggedElementsAtIndex(draggedRows, _treeModel.Root, _treeModel.Root.Children.Count);

						return DragAndDropVisualMode.Move;
					}
				default:
					Debug.LogError("Unhandled enum " + args.dragAndDropPosition);
					return DragAndDropVisualMode.None;
			}
		}

		public void OnDropDraggedElementsAtIndex (List<TreeViewItem> draggedRows, T parent, int insertIndex)
		{
			if (BeforeDroppingDraggedItems != null)
				BeforeDroppingDraggedItems (draggedRows);

			var draggedElements = new List<TreeElement> ();
			foreach (var x in draggedRows)
				draggedElements.Add (((TreeViewItem<T>) x).Data);

			var selectedIDs = draggedElements.Select (x => x.Id).ToArray();
			_treeModel.MoveElements (parent, insertIndex, draggedElements);
			SetSelection(selectedIDs, TreeViewSelectionOptions.RevealAndFrame);
		}


		bool ValidDrag(TreeViewItem parent, List<TreeViewItem> draggedItems)
		{
			TreeViewItem currentParent = parent;
			while (currentParent != null)
			{
				if (draggedItems.Contains(currentParent))
					return false;
				currentParent = currentParent.parent;
			}
			return true;
		}

	}

	/// <summary>
	/// TreeViewItem class which will be used by your TreeViewWithTreeModel using the same TreeElement generic provided by your TreeModel
	/// </summary>
	/// <typeparam name="T">a TreeElement generic class</typeparam>
	internal class TreeViewItem<T> : TreeViewItem where T : TreeElement
	{
		public T Data { get; }

		public TreeViewItem (int id, int depth, string displayName, T data) : base (id, depth, displayName)
		{
			Data = data;
		}
	}

}
