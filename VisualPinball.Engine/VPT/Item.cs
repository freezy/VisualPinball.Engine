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

namespace VisualPinball.Engine.VPT
{
	/// <summary>
	/// The base class for all playfield items (including the table itself)
	/// </summary>
	/// <typeparam name="TData">Data class type this item is using</typeparam>
	public class Item<TData> : IItem where TData : ItemData
	{
		public readonly TData Data;

		public string Name { get => Data.GetName(); set => Data.SetName(value); }
		public int Index { get; set; }
		public int Version { get; set; }
		public int StorageIndex { get => Data.StorageIndex; set => Data.StorageIndex = value; }

		protected Item(TData data)
		{
			Data = data;
		}
	}
}
