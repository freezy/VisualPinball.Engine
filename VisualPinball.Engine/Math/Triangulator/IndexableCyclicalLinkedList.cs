// Triangulator
//
// The MIT License (MIT)
//
// Copyright (c) 2017, Nick Gravelyn
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

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
