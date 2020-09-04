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
		/// This Unique Id is automatically set & increment on each TreeElement CTor so you don't need to worry about its unicity
		/// </summary>
		/// <remarks>
		/// Could be useful when constructing an array of TreeElemnt in a delegate for instance, without having to manage a local counter
		/// Of course, this Id can be overridden afterward
		/// </remarks>
		private static int UniqueId = typeof(TreeElement).GUID.ToString().GetHashCode();

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

		public TreeElement()
		{
			Id = UniqueId++;
		}

		public TreeElement(int id)
		{
			Id = id;
		}

		public void AddChild(TreeElement child)
		{
			child?.ReParent(this);
		}

		public void AddChildren(TreeElement[] children)
		{
			foreach (var child in children) {
				child.ReParent(this);
			}
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


