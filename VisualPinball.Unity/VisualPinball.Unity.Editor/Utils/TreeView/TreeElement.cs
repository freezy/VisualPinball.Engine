using System;
using System.Collections.Generic;
using UnityEngine;

namespace VisualPinball.Unity.Editor.Utils.TreeView
{
	/// <summary>
	/// TreeElement is the base class for creating TreeModel which will be used by TreeViewWithTreeModel, you'll have to inherit from it.
	/// It provides base properties to be inserted & identified in a TreeView (Id, Name, Depth)
	/// It hosts also its hierarchical information.
	/// </summary>
	public abstract class TreeElement
	{
		/// <summary>
		/// Unique ID of the TreeElement within the Tree structure
		/// </summary>
		public int Id { get; set; }
		/// <summary>
		/// The element's Name
		/// </summary>
		public virtual string Name { get; }
		/// <summary>
		/// The name which will be displayed in the TreeView 
		/// </summary>
		public virtual string DisplayName => Name;
		/// <summary>
		/// The icon which will be shown before the DisplayName
		/// </summary>
		public virtual Texture2D Icon => null;
		/// <summary>
		/// The Depth level of the TreeElement into the Tree structure
		/// </summary>
		public int Depth { get; set; }
		/// <summary>
		/// This TreeElement's parent
		/// </summary>
		public TreeElement Parent { get; set; }
		/// <summary>
		/// This TreeElement's Children
		/// </summary>
		public List<TreeElement> Children { get; set; } = new List<TreeElement>();
		public bool HasChildren => Children != null && Children.Count > 0;

		/// <summary>
		/// This helper try to find an element with the provided id in this TreeElement's hierarchy, including itself
		/// </summary>
		/// <typeparam name="T">a TreeElement generic class</typeparam>
		/// <param name="id">the id of the searched element</param>
		/// <returns>casted TreeElement as T, or null if not found</returns>
		public T Find<T>(int id) where T : TreeElement
		{
			if (Id == id) {
				return (T)this;
			}

			foreach(var child in Children) {
				var itFound = child.Find<T>(id);
				if (itFound != null) {
					return itFound;
				}
			}

			return null;
		}

		public delegate bool ChildFilter(TreeElement child);
		/// <summary>
		/// Will gather all children from this TreeElement which pass the provided filter
		/// </summary>
		/// <typeparam name="T">a generic TreeElement child class</typeparam>
		/// <param name="filter">a filtering delegate</param>
		/// <returns>an array of TreeElement casted as T type</returns>
		public T[] GetChildren<T>(ChildFilter filter) where T : TreeElement
		{
			List<T> children = new List<T>();
			foreach(var child in Children) {
				if (filter(child)) {
					children.Add((T)child);
				}
				children.AddRange(child.GetChildren<T>(filter));
			}
			return children.ToArray();
		}
	}
}


