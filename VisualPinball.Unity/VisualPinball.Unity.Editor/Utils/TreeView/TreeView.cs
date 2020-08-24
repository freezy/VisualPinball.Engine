using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// An ImGui TreeView which will handle a provided generic TreeElement type
	/// It will handle all TreeViewItem creation using this generic TreeElement type
	/// You have to provide the Root element of your tree structure
	/// It'll also fire events for several base TreeView events (Tree rebuild & update, item double click...)
	/// </summary>
	/// <typeparam name="T">a TreeElement generic class</typeparam>
	internal class TreeView<T> : TreeView where T : TreeElement
	{
		/// <summary>
		/// Name of the DragAndDrop GenericData which will be set for transfering item ids
		/// </summary>
		protected const string DragAndDropItemsTask = "TreeViewItemsTask";

		#region Events
		public event Action<T> TreeRebuilt;
		public event Action<T[]> ItemDoubleClicked;
		public event Action<T[]> ItemContextClicked;
		#endregion

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

		#region TreeViewItem management
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
		#endregion

		#region Search
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
		#endregion

		#region Item Renaming
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
		#endregion

		#region Item Clicking
		/// <summary>
		/// Called when a right click is made into the TreeView rect but not on any item
		/// </summary>
		protected override void ContextClicked()
		{
			base.ContextClicked();
			var itemsId = GetSelection();
			var elements = Root.GetChildren<T>(e => itemsId.Contains(e.Id));
			ItemContextClicked?.Invoke(elements);
		}

		/// <summary>
		/// override of the DoubleClickedItem to fire registered events with the attached TreeElement
		/// </summary>
		/// <param name="id"></param>
		protected override void DoubleClickedItem(int id)
		{
			base.DoubleClickedItem(id);
			var selectedItems = GetSelection().Select(Id => Root.Find<T>(Id));
			ItemDoubleClicked?.Invoke(selectedItems.ToArray());
		}
		#endregion

		#region Drag & Drop
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
			DragAndDrop.SetGenericData(DragAndDropItemsTask, args.draggedItemIDs);
			DragAndDrop.StartDrag($"{args.draggedItemIDs.Count} tree item(s)");
		}

		protected virtual DragAndDropVisualMode HandleElementsDragAndDrop(DragAndDropArgs args, T[] elements) => DragAndDropVisualMode.Generic;
		protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
		{
			//Get Id list from GenericData if the DragAndDrop was initiated by the TreeView
			var idList = DragAndDrop.GetGenericData(DragAndDropItemsTask) as IList<int>;

			if (args.performDrop) {
				DragAndDrop.SetGenericData(DragAndDropItemsTask, null);
			}

			if (idList == null) {
				//Check for gameObjects inside the DragAndDrop task (could come from the SceneHierarchy)
				idList = DragAndDrop.objectReferences.Select(obj => obj.GetInstanceID()).ToList();
			}

			if (idList != null) {
				var elements = Root.GetChildren<T>(element => idList.Contains(element.Id));
				return HandleElementsDragAndDrop(args, elements.ToArray());
			}

			return DragAndDropVisualMode.None;
		}
		#endregion
	}

	/// <summary>
	/// TreeViewItem class which will be used by your <see cref="TreeView{T}"/> using the same TreeElement generic 
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
