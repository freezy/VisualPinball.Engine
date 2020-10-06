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

// ReSharper disable InconsistentNaming

using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The base class for all authoring components on the playfield.<p/>
	/// </summary>
	/// <typeparam name="TItem"></typeparam>
	/// <typeparam name="TData"></typeparam>
	public abstract class ItemAuthoring<TItem, TData> : MonoBehaviour
		where TData : ItemData
		where TItem : Item<TData>, IRenderable
	{
		public string Name { get => Item.Name; set => Item.Name = value; }

		/// <summary>
		/// Returns the data object relevant for this component. If this
		/// returns `null`, then it's wrongly attached to a game object
		/// where it can't find its main component.
		/// </summary>
		///
		/// <remarks>
		/// The default implementation here represents the one of a main
		/// component. It's overridden for the sub components (<see cref="ItemColliderAuthoring{TItem,TData,TAuthoring}"/>, etc)
		/// </remarks>
		public abstract TData Data { get; }

		/// <summary>
		/// Returns the item object for this component.
		/// </summary>
		///
		/// <remarks>
		/// If this returns `null`, then it's wrongly attached to a game object
		/// where it can't find its main component.
		/// </remarks>
		public abstract TItem Item { get; }

		/// <summary>
		/// A non-typed version of the item.
		/// </summary>
		public IItem IItem => Item;

		/// <summary>
		/// The data-oriented version of the item.
		/// </summary>
		public ItemData ItemData => Data;

		public string ItemType => Item.ItemName;

		public bool IsLocked { get => Data.IsLocked; set => Data.IsLocked = value; }

		private Table _table;

		protected Table Table => _table ?? (_table = gameObject.transform.GetComponentInParent<TableAuthoring>()?.Item);

		protected virtual void ItemDataChanged()
		{
		}
	}
}
