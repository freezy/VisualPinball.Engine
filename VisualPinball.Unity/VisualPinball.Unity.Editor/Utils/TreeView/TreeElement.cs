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

		public void AddChild(TreeElement child)
		{
			AddChildren(new TreeElement[] { child });
		}
		public void AddChildren(TreeElement[] children)
		{
			foreach(var child in children) {
				child.ReParent(this);
			}
		}

		public virtual void ReParent(TreeElement newParent)
		{
			Parent?.Children.Remove(this);
			newParent.Children.Add(this);
			Parent = newParent;
			UpdateDepth();
		}

		private void UpdateDepth()
		{
			Depth = Parent != null ? Parent.Depth + 1 : 0;
			foreach (var child in Children) {
				child.UpdateDepth();
			}
		}
	}
}


