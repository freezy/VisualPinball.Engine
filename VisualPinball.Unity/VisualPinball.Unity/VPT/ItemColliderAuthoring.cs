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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public abstract class ItemColliderAuthoring<TItem, TData, TAuthoring> : ItemAuthoring<TItem, TData>, IItemColliderAuthoring
		where TData : ItemData
		where TItem : Item<TData>, IHittable, IRenderable
		where TAuthoring : ItemAuthoring<TItem, TData>
	{
		/// <summary>
		/// We're in a sub component here, so in order to retrieve the data,
		/// this will:
		/// 1. Check if <see cref="ItemAuthoring{TItem,TData}._data"/> is set (e.g. it's a serialized item)
		/// 2. Find the main component in the hierarchy and return its data.
		/// </summary>
		///
		/// <remarks>
		/// We deliberately don't cache this, because if we do we need to find
		/// a way to invalidate the cache in case the game object gets
		/// re-attached to another parent.
		/// </remarks>
		public override TData Data => _data ?? FindData();

		/// <summary>
		/// Since we're in a sub component, we don't instantiate the item, but
		/// look for the main component and retrieve the item from there (which
		/// will instantiate it itself if necessary).
		/// </summary>
		///
		/// <remarks>
		/// If no main component found, this yields to `null`, and in this case
		/// the component is somewhere in the hierarchy where it doesn't make
		/// sense, and a warning should be printed.
		/// </remarks>
		public override TItem Item => _item ?? FindItem();

		public bool ShowColliderMesh;
		public bool ShowAabbs;

		protected override string[] Children => new string[0];

		public IItemAuthoring SetItem(TItem item, RenderObjectGroup rog)
		{
			_item = item;
			_data = item.Data;
			name = rog.ComponentName + " (collider)";
			return this;
		}

		private TData FindData()
		{
			var ac = FindParentAuthoring();
			return ac != null ? ac.Data : null;
		}

		private TItem FindItem()
		{
			var ac = FindParentAuthoring();
			return ac != null ? ac.Item : null;
		}

		private TAuthoring FindParentAuthoring()
		{
			var go = gameObject;

			// search on current game object
			var ac = go.GetComponent<TAuthoring>();
			if (ac != null) {
				return ac;
			}

			// search on parent
			if (go.transform.parent != null) {
				ac = go.transform.parent.GetComponent<TAuthoring>();
			}
			if (ac != null) {
				return ac;
			}

			// search on grand parent
			if (go.transform.parent.transform.parent != null) {
				ac = go.transform.parent.transform.parent.GetComponent<TAuthoring>();
			}

			if (ac == null) {
				Debug.LogWarning("No same- or parent authoring component found.");
			}

			return ac;
		}
	}
}
