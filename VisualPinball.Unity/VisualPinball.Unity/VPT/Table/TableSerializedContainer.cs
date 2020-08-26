using System.Collections;
using System.Collections.Generic;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.VPT.Table
{
	/// <summary>
	/// Base container class to handle in a Dictionnary-like way objects based on <see cref="TableSerializedData{TData}"/> generic class
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
		public IEnumerable<T> Values => _data.Values;
		public IEnumerable<TSerialized> SerializedObjects => _serializedData;

		[UnityEngine.SerializeField] protected List<TSerialized> _serializedData = new List<TSerialized>();
		[UnityEngine.SerializeField] protected bool _dictDirty = false;
		protected Dictionary<string, T> Data => _data == null || _dictDirty ? (_data = CreateDict()) : _data;
		protected Dictionary<string, T> _data = null;

		public T this[string k] => Get(k);
		public T Get(string k)
		{
			Data.TryGetValue(k.ToLower(), out T val);
			return val;
		}

		protected abstract Dictionary<string, T> CreateDict();
		public abstract void Add(T value);

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
			Data.Remove(lowerName);
			return found;
		}

		public void SetNameMapDirty()
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
