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

using System.Collections;
using System.Collections.Generic;

namespace VisualPinball.Engine.VPT.Table
{
	/// <summary>
	/// Provides a basic default implementation for ITableResourceContainer that stores T in a c# dict
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public class DefaultTableResourceContainer<T> : ITableResourceContainer<T> where T : IItem
	{
		private Dictionary<string, T> _dict = new Dictionary<string, T>();

		public int Count => _dict.Count;
		public IEnumerable<T> Values => _dict.Values;

		public T this[string k] => Get(k);
		public T Get(string k)
		{
			_dict.TryGetValue(k.ToLower(), out T val);
			return val;
		}
		public void Add(T value) => _dict[value.Name.ToLower()] = value;
		public bool Remove(T value) => _dict.Remove(value.Name.ToLower());

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		public IEnumerator<T> GetEnumerator()
		{
			foreach (var kvp in _dict) {
				yield return kvp.Value;
			}
		}
	}
}
