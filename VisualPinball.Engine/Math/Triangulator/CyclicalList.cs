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
	/// Implements a List structure as a cyclical list where indices are wrapped.
	/// </summary>
	/// <typeparam name="T">The Type to hold in the list.</typeparam>
	internal class CyclicalList<T> : List<T>
	{
		public new T this[int index]
		{
			get
			{
				//perform the index wrapping
				while (index < 0)
					index = Count + index;
				if (index >= Count)
					index %= Count;

				return base[index];
			}
			set
			{
				//perform the index wrapping
				while (index < 0)
					index = Count + index;
				if (index >= Count)
					index %= Count;

				base[index] = value;
			}
		}

		public CyclicalList() { }

		public CyclicalList(IEnumerable<T> collection)
			: base(collection)
		{
		}

		public new void RemoveAt(int index)
		{
			Remove(this[index]);
		}
	}
}
