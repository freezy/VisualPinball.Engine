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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Base container class to handle in a Dictionary-like way objects based on <see cref="TableSerializedData{TData}"/> generic class
	/// </summary>
	/// <typeparam name="T">IItem derived class encapsulating the handled ItemData object</typeparam>
	/// <typeparam name="TData">ItemData derived class which will be encapsulated in a <see cref="TableSerializedData{TData}"/></typeparam>
	/// <typeparam name="TSerialized">The full description of the <see cref="TableSerializedData{TData}"/> which will be used in this container</typeparam>
	public abstract class TableSerializedContainer<T, TData, TSerialized> : ITableResourceContainer<T>
		where T : IItem
		where TData : ItemData
		where TSerialized : TableSerializedData<TData>
	{
		public int Count => _serializedData.Count;
		public IEnumerable<T> Values => Data.Values;
		public IEnumerable<TSerialized> SerializedObjects => _serializedData;

		[UnityEngine.SerializeField]
		protected readonly List<TSerialized> _serializedData = new List<TSerialized>();

		[UnityEngine.SerializeField]
		protected bool _dictDirty;

		private Dictionary<string, T> Data => _data == null || _dictDirty ? _data = CreateDict() : _data;

		private Dictionary<string, T> _data;

		public T this[string k] => Get(k);
		public T Get(string k)
		{
			Data.TryGetValue(k.ToLower(), out T val);
			return val;
		}

		protected abstract Dictionary<string, T> CreateDict();
		protected abstract bool TryAddSerialized(T value);

		public void Add(T value)
		{
			Remove(value);
			if (TryAddSerialized(value)) {
				Data[value.Name.ToLower()] = value;
				SetNameMapDirty();
			}
		}

		public void AddRange(ITableResourceContainer<T> values)
		{
			foreach(var value in values) {
				Add(value);
			}
		}

		public bool Remove(T value)
		{
			return Remove(value.Name);
		}

		public bool Remove(string name)
		{
			string lowerName = name.ToLower();
			bool found = false;
			for (int i = 0; i < Data.Count; i++) {
				if (_serializedData[i].Data.GetName().ToLower() == lowerName) {
					_serializedData.RemoveAt(i);
					found = true;
					break;
				}
			}
			if (found) {
				Data.Remove(lowerName);
				SetNameMapDirty();
			}
			return found;
		}

		public bool Move(string name, int newIdx)
		{
			if (newIdx < 0 || newIdx > _serializedData.Count - 1) {
				return false;
			}

			var foundItem = _serializedData.Where(d => string.Compare(d.Data.GetName(), name, StringComparison.InvariantCultureIgnoreCase) == 0).ToArray();
			if (foundItem.Length == 1) {
				_serializedData.Remove(foundItem[0]);
				_serializedData.Insert(newIdx, foundItem[0]);
				SetNameMapDirty();
				return true;
			}

			return false;
		}

		protected void SetNameMapDirty()
		{
			_dictDirty = true;
		}


		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		public IEnumerator<T> GetEnumerator()
		{
			foreach (var kvp in Data) {
				yield return kvp.Value;
			}
		}
	}
}
