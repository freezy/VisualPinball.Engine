using System;
using System.Collections.Generic;

namespace VisualPinball.Unity.Editor
{

	/// <summary>
	/// TreeElementUtility and TreeElement are useful helper classes for backend tree data structures.
	/// </summary>
	public static class TreeElementUtility
	{
		#region TreeElement Helpers       
		public delegate bool ChildFilter<T>(T child) where T : TreeElement;

		/// <summary>
		/// This helper try to find an element with the provided id in this TreeElement's hierarchy, including itself
		/// </summary>
		/// <typeparam name="T">a TreeElement generic class</typeparam>
		/// <param name="rootElement">the root element for the search</param>
		/// <param name="filter">the filtering predicate</param>
		/// <returns>casted TreeElement as T, or null if not found</returns>
		public static T Find<T>(this T rootElement, ChildFilter<T> filter) where T : TreeElement
		{
			if (filter.Invoke(rootElement as T)) {
				return rootElement;
			}

			foreach (var child in rootElement.Children) {
				var itFound = ((T)child).Find<T>(filter);
				if (itFound != null) {
					return itFound;
				}
			}
			return null;
		}
		/// <summary>
		/// Find helper to search an element within provided TreeElement hierarchy based on its Id
		/// </summary>
		/// <typeparam name="T">a generic TreeElement child class</typeparam>
		/// <param name="rootElement">the root element for the search</param>
		/// <param name="id">the id of the element to search</param>
		/// <returns></returns>
		public static T Find<T>(this T rootElement, int id) where T : TreeElement => Find<T>(rootElement, d => d.Id == id);

		/// <summary>
		/// Find helper to search an element within provided TreeElement hierarchy 
		/// </summary>
		/// <typeparam name="T">a generic TreeElement child class</typeparam>
		/// <param name="rootElement">the root element for the search</param>
		/// <param name="element">the element to search</param>
		/// <returns></returns>
		public static T Find<T>(this T rootElement, T element) where T : TreeElement => Find<T>(rootElement, d => d == element);

		/// <summary>
		/// Will gather all children from this TreeElement which pass the provided filter
		/// </summary>
		/// <typeparam name="T">a generic TreeElement child class</typeparam>
		/// <param name="element">the root element for the parsing</param>
		/// <param name="filter">a filtering delegate</param>
		/// <returns>an array of TreeElement as T type</returns>
		public static T[] GetChildren<T>(this T rootElement, ChildFilter<T> filter) where T : TreeElement
		{
			List<T> children = new List<T>();
			foreach (var child in rootElement.Children) {
				if (filter.Invoke(child as T)) {
					children.Add(child as T);
				}
				children.AddRange(((T)child).GetChildren<T>(filter));
			}
			return children.ToArray();
		}

		/// <summary>
		/// Will gather all children from this TreeElement 
		/// </summary>
		/// <typeparam name="T">a generic TreeElement child class</typeparam>
		/// <param name="rootElement">the root element for the parsing</param>
		/// <returns>an array of TreeElement as T type</returns>
		public static T[] GetChildren<T>(this T rootElement) where T : TreeElement => GetChildren<T>(rootElement, d => true);
		#endregion

		#region Tree structure helpers
		/// <summary>
		/// Will contruct a hierarchical tree from a flat sorted list of TreeElements
		/// </summary>
		/// <typeparam name="T">the TreeElement derived class you'll use for your TreeView</typeparam>
		/// <param name="list">a flattened list of TreeElement</param>
		/// <returns>the root of the tree parsed from the list (always the first element).</returns>
		/// <remarks>
		/// Important: the first item and is required to have a depth value of -1.
		/// The rest of the items should have depth >= 0.
		/// </remarks>
		public static T ListToTree<T>(IList<T> list) where T : TreeElement
		{
			// Validate input
			ValidateDepthValues (list);

			// Clear old states
			foreach (var element in list)
			{
				element.Parent = null;
				element.Children = null;
			}

			// Set child and parent references using depth info
			for (int parentIndex = 0; parentIndex < list.Count; parentIndex++)
			{
				var parent = list[parentIndex];
				bool alreadyHasValidChildren = parent.Children != null;
				if (alreadyHasValidChildren)
					continue;

				int parentDepth = parent.Depth;
				int childCount = 0;

				// Count children based depth value, we are looking at children until it's the same depth as this object
				for (int i = parentIndex + 1; i < list.Count; i++)
				{
					if (list[i].Depth == parentDepth + 1)
						childCount++;
					if (list[i].Depth <= parentDepth)
						break;
				}

				// Fill child array
				List<TreeElement> childList = null;
				if (childCount != 0)
				{
					childList = new List<TreeElement>(childCount); // Allocate once
					childCount = 0;
					for (int i = parentIndex + 1; i < list.Count; i++)
					{
						if (list[i].Depth == parentDepth + 1)
						{
							list[i].Parent = parent;
							childList.Add(list[i]);
							childCount++;
						}

						if (list[i].Depth <= parentDepth)
							break;
					}
				}

				parent.Children = childList;
			}

			return list[0];
		}

		/// <summary>
		/// Will check all depth values in a flatten items list
		/// </summary>
		/// <param name="list">a flattened list of TreeElement</param>
		public static void ValidateDepthValues<T>(IList<T> list) where T : TreeElement
		{
			if (list.Count == 0)
				throw new ArgumentException("list should have items, count is 0, check before calling ValidateDepthValues", "list");

			if (list[0].Depth != -1)
				throw new ArgumentException("list item at index 0 should have a depth of -1 (since this should be the hidden root of the tree). Depth is: " + list[0].Depth, "list");

			for (int i = 0; i < list.Count - 1; i++)
			{
				int depth = list[i].Depth;
				int nextDepth = list[i + 1].Depth;
				if (nextDepth > depth && nextDepth - depth > 1)
					throw new ArgumentException(string.Format("Invalid depth info in input list. Depth cannot increase more than 1 per row. Index {0} has depth {1} while index {2} has depth {3}", i, depth, i + 1, nextDepth));
			}

			for (int i = 1; i < list.Count; ++i)
				if (list[i].Depth < 0)
					throw new ArgumentException("Invalid depth value for item at index " + i + ". Only the first item (the root) should have depth below 0.");

			if (list.Count > 1 && list[1].Depth != 0)
				throw new ArgumentException("Input list item at index 1 is assumed to have a depth of 0", "list");
		}
		#endregion
	}

}
