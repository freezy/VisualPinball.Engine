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
