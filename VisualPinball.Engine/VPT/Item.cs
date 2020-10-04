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

namespace VisualPinball.Engine.VPT
{
	public enum ItemSubComponent
	{
		None, Collider, Mesh
	}

	/// <summary>
	/// The base class for all playfield items (including the table itself)
	/// </summary>
	/// <typeparam name="TData">Data class type this item is using</typeparam>
	public abstract class Item<TData> : IItem where TData : ItemData
	{
		public abstract string ItemName { get; }
		public abstract string ItemGroupName { get; }

		public readonly TData Data;

		public string Name
		{
			get => Data.GetName();
			set
			{
				Data.SetName(value);
				(ComponentName, SubComponent, SubName) = SplitName();
			}
		}

		public int Index { get; set; }
		public int Version { get; set; }
		public int StorageIndex { get => Data.StorageIndex; set => Data.StorageIndex = value; }

		public string ComponentName { get; private set; }
		public ItemSubComponent SubComponent { get; private set; }
		public string SubName { get; private set; }

		protected Item(TData data)
		{
			Data = data;
		}

		private (string, ItemSubComponent, string) SplitName()
		{
			var names = Name.Split(new[] {'_'}, 3, StringSplitOptions.None);
			if (names.Length == 1) {
				return (Name, ItemSubComponent.None, null);
			}
			switch (names[1].ToLower()) {
				case "collider":
					return (names[0], ItemSubComponent.Collider, names.Length > 2 ? names[2] : null);

				case "mesh":
					return (names[0], ItemSubComponent.Mesh, names.Length > 2 ? names[2] : null);

				default:
					return (Name, ItemSubComponent.None, null);
			}
		}
	}
}
