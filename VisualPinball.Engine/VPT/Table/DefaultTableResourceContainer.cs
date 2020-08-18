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
