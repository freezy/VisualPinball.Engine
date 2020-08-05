using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;


namespace VisualPinball.Unity.Editor.Utils.TreeView
{
	/// <summary>
	/// The TreeWiew class is a ImGui TreeView which will handle a provided generic TreeElement type
	/// It will handle all TreeViewItem creation using this generic TreeElement type
	/// You have to provide the Root element of your tree structure
	/// It'll also fire events for several base TreeView events (Tree rebuild & update, item double click...)
	/// </summary>
	/// <typeparam name="T">a TreeElement generic class</typeparam>
	internal class TreeView<T> : UnityEditor.IMGUI.Controls.TreeView where T : TreeElement
	{
		public event Action<T> TreeRebuilt;
		public event Action<T, IList<T>, T, int> TreeChanged;
		public event Action<T> ItemDoubleClicked;
		public event Action<T> ItemContextClicked;

		public T Root { get; private set; }
		protected readonly List<TreeViewItem> _rows = new List<TreeViewItem>();

		protected TreeView(TreeViewState state, T root) : base(state)
		{
			Init(root);
		}

		private void Init(T root)
		{
			Root = root;
		}

		public void SetData(T root)
		{
			SetData(root);
			TreeRebuilt?.Invoke(Root);
			Reload();
		}
		public void SetData(IList<T> elements)
		{
			SetData(TreeElementUtility.ListToTree<T>(elements));
			TreeRebuilt?.Invoke(Root);
			Reload();
		}

		private void HierachyChanged(IList<TreeViewItem<T>> itemsMoved, TreeViewItem<T> newParent, int insertionIndex)
		{
			IList<T> elementsMoved = new List<T>(itemsMoved.Select(it => it.Data));
			TreeChanged?.Invoke(Root, elementsMoved, newParent.Data, insertionIndex);
			Reload();
		}

		protected override TreeViewItem BuildRoot()
			=> new TreeViewItem<T>(Root.Id, -1, Root);

		protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
		{
			if (Root == null) {
				throw new InvalidOperationException("Tree root is null. Did you call SetData()?");
			}

			_rows.Clear();
			if (!string.IsNullOrEmpty(searchString)) {
				Search(Root, searchString, _rows);
			} else {
				if (Root.HasChildren) {
					AddChildrenRecursive(Root, 0, _rows);
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
				var item = new TreeViewItem<T>(child.Id, depth, child);
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

		/// <summary>
		/// Thi virtual method let your derived TreeView class validate wether or not an element should be kept while using search feature
		/// </summary>
		/// <param name="search">the text to search</param>
		/// <param name="element">the evaluated TreeElement</param>
		/// <returns>true is the TreeViewElement matches the search, false otherwise</returns>
		protected virtual bool ValidateSearch(string search, T element) => element.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

		void Search(T searchFromThis, string search, List<TreeViewItem> result)
		{
			if (string.IsNullOrEmpty(search)) {
				throw new ArgumentException("Invalid search: cannot be null or empty", "search");
			}

			Stack<T> stack = new Stack<T>();
			foreach (var element in searchFromThis.Children) {
				stack.Push((T)element);
			}

			while (stack.Count > 0) {
				T current = stack.Pop();
				// Matches search?
				if (ValidateSearch(search, current)) {
					result.Add(new TreeViewItem<T>(current.Id, 0, current));
				}

				if (current.Children != null && current.Children.Count > 0) {
					foreach (var element in current.Children) {
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

		/// <summary>
		/// This virtual method let your derived TreeView class to validate if a TreeViewItem can be renamed
		/// </summary>
		/// <param name="item">The evaluated TreeViewItem, already using the derived TreeElement generic</param>
		/// <returns>true if this item can be renamed, false otherwise</returns>
		protected virtual bool ValidateRename(TreeViewItem<T> item) => true;
		protected override bool CanRename(TreeViewItem item)
		{
			if (item is TreeViewItem<T> treeViewItem) {
				if (ValidateRename(treeViewItem)) {
					//reset displayName to TreeElement Name before renaming (displayName could have been customized by TreeElement.DisplayName)
					treeViewItem.displayName = treeViewItem.Data.Name;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Called when a right click is made into the TreeView rect but not on any item
		/// </summary>
		protected override void ContextClicked()
		{
			base.ContextClicked();
			ItemContextClicked?.Invoke(null);
		}

		/// <summary>
		/// Called when a right click is made on a TreeViewItem
		/// </summary>
		/// <param name="id">the TreeViewItem id</param>
		/// <remarks>
		/// if no Action consume the Event.current, a ContextClicked will be called afterward
		/// </remarks>
		protected override void ContextClickedItem(int id)
		{
			base.ContextClickedItem(id);
			var item = Root.Find<T>(id);
			if (item != null) {
				ItemContextClicked?.Invoke(item);
			}
		}

		/// <summary>
		/// override of the DoubleClickedItem to fire registered events with the attached TreeElement
		/// </summary>
		/// <param name="id"></param>
		protected override void DoubleClickedItem(int id)
		{
			base.DoubleClickedItem(id);
			var item = Root.Find<T>(id);
			if (item != null) {
				ItemDoubleClicked?.Invoke(item);
			}
		}

		protected virtual bool ValidateStartDrag(T[] elements) => false;
		protected override bool CanStartDrag(CanStartDragArgs args)
		{
			List<T> elements = new List<T>();
			elements.AddRange(Root.GetChildren<T>(element => element.Id == args.draggedItem?.id).ToList<T>());
			elements.AddRange(Root.GetChildren<T>(element => args.draggedItemIDs.Contains(element.Id)).ToList<T>());
			return ValidateStartDrag(elements.ToArray());
		}

		protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
		{
			DragAndDrop.PrepareStartDrag();

			List<string> idStrList = new List<string>();
			foreach (var id in args.draggedItemIDs) {
				idStrList.Add(id.ToString());
			}
			DragAndDrop.paths = idStrList.ToArray();

			DragAndDrop.StartDrag($"{args.draggedItemIDs.Count} tree item(s)");

			Reload();
		}

		protected virtual DragAndDropVisualMode HandleElementsDragAndDrop(DragAndDropArgs args, T[] elements) => DragAndDropVisualMode.Generic;
		protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
		{
			List<int> idList = new List<int>();
			foreach (var idStr in DragAndDrop.paths) {
				idList.Add(Int32.Parse(idStr));
			}
			var elements = Root.GetChildren<T>(element => idList.Contains(element.Id));
			return HandleElementsDragAndDrop(args, elements.ToArray());
		}

	}

	/// <summary>
	/// TreeViewItem class which will be used by your TreeViewWithTreeModel using the same TreeElement generic provided by your TreeModel
	/// </summary>
	/// <typeparam name="T">a TreeElement generic class</typeparam>
	internal class TreeViewItem<T> : TreeViewItem where T : TreeElement
	{
		public T Data { get; }

		public TreeViewItem(int id, int depth, T data) : base(id, depth, data?.DisplayName)
		{
			Data = data;
			icon = data?.Icon;
		}
	}

}
