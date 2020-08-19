using System.Collections.Generic;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// TreeElement is the base class used by <see cref="TreeView{T}"/>, you'll have to inherit from it.
	/// It provides base properties to be inserted & identified in a TreeView (Id, Name, Depth)
	/// It hosts also its hierarchical information.
	/// </summary>
	public abstract class TreeElement
	{
		/// <summary>
		/// Unique ID of this <see cref="TreeElement"/> within the tree structure
		/// </summary>
		public int Id { get; set; }

		/// <summary>
		/// The element's name
		/// </summary>
		public abstract string Name { get; }

		/// <summary>
		/// The name which is displayed in the <see cref="TreeView"/>
		/// </summary>
		public virtual string DisplayName => Name;

		/// <summary>
		/// The icon which is shown before the <see cref="DisplayName"/>
		/// </summary>
		public virtual Texture2D Icon => null;

		/// <summary>
		/// The depth level of this <see cref="TreeElement"/> into the tree structure
		/// </summary>
		public int Depth { get; set; }

		/// <summary>
		/// The parent of this <see cref="TreeElement"/>
		/// </summary>
		public TreeElement Parent { get; set; }

		/// <summary>
		/// The children of this <see cref="TreeElement"/>
		/// </summary>
		public List<TreeElement> Children { get; set; } = new List<TreeElement>();

		public bool HasChildren => Children != null && Children.Count > 0;

		public void AddChild(TreeElement child)
		{
			child?.ReParent(this);
		}

		public virtual TreeElement ReParent(TreeElement newParent)
		{
			var oldParent = Parent;
			oldParent?.Children.Remove(this);
			newParent?.Children.Add(this);
			Parent = newParent;
			UpdateDepth();
			return oldParent;
		}

		private void UpdateDepth()
		{
			Depth = Parent?.Depth + 1 ?? 0;
			foreach (var child in Children) {
				child.UpdateDepth();
			}
		}
	}
}


