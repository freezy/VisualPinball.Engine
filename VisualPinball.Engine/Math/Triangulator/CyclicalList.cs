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
