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

namespace VisualPinball.Engine.Math.Triangulator
{
	/// <summary>
	/// Implements a LinkedList that is both indexable as well as cyclical. Thus
	/// indexing into the list with an out-of-bounds index will automatically cycle
	/// around the list to find a valid node.
	/// </summary>
	internal class IndexableCyclicalLinkedList<T> : LinkedList<T>
	{
		/// <summary>
		/// Gets the LinkedListNode at a particular index.
		/// </summary>
		/// <param name="index">The index of the node to retrieve.</param>
		/// <returns>The LinkedListNode found at the index given.</returns>
		public LinkedListNode<T> this[int index]
		{
			get
			{
				//perform the index wrapping
				while (index < 0)
					index = Count + index;
				if (index >= Count)
					index %= Count;

				//find the proper node
				LinkedListNode<T> node = First;
				for (int i = 0; i < index; i++)
					node = node.Next;

				return node;
			}
		}

		/// <summary>
		/// Removes the node at a given index.
		/// </summary>
		/// <param name="index">The index of the node to remove.</param>
		public void RemoveAt(int index)
		{
			Remove(this[index]);
		}

		/// <summary>
		/// Finds the index of a given item.
		/// </summary>
		/// <param name="item">The item to find.</param>
		/// <returns>The index of the item if found; -1 if the item is not found.</returns>
		public int IndexOf(T item)
		{
			for (int i = 0; i < Count; i++)
				if (this[i].Value.Equals(item))
					return i;

			return -1;
		}
	}
}
