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

using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	/// <summary>
	/// ScriptableObject generic wrapper for any data which needs to be managed indepenently, e.g. for Undo tracking.
	/// These objects types can be handled by <see cref="TableSerializedContainer{T, TData}"/>
	/// </summary>
	/// <typeparam name="TData">The ItemData based class which will be wrapped into a ScriptableObject</typeparam>
	/// <remarks>
	/// These wrapper are used by <see cref="TableSidecar"/> for Textures, Sounds to avoid undo operations on the whole structure
	/// </remarks>
	public class TableSerializedData<TData> : ScriptableObject where TData : ItemData
	{
		public TData Data;

		public static T GenericCreate<T>(TData data) where T : TableSerializedData<TData>
		{
			var tst = CreateInstance<T>();
			tst.Data = data;
			return tst;
		}
	}
}
