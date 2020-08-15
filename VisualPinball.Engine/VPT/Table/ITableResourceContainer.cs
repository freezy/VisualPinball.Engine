using System.Collections.Generic;

namespace VisualPinball.Engine.VPT.Table
{
	/// <summary>
	/// Dictionary-like interface for table global resources (such as images/textures)
	/// Does not provide arbitrary key access, instead all access is implicit based on INameable.Name
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public interface ITableResourceContainer<T> : IEnumerable<T> where T : IItem
	{
		int Count { get; }
		IEnumerable<T> Values { get; }
		T this[string k] { get; }
		T Get(string k);
		void Add(T value);
		bool Remove(T value);
	}
}
