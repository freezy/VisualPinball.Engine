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
		public override TData Data => _data ?? (_data = FindData());

		public override TItem Item => _item ?? (_item = FindItem());

		public IItemAuthoring SetItem(TItem item, RenderObjectGroup rog)
		{
			_data = item.Data;
			name = rog.ComponentName + " (collider)";
			return this;
		}

		protected override string[] Children => new string[0];

		private TItem FindItem()
		{
			var ac = FindParentAuthoring();
			return ac != null ? ac.Item : null;
		}

		private TData FindData()
		{
			var ac = FindParentAuthoring();
			return ac != null ? ac.Data : null;
		}

		private TAuthoring FindParentAuthoring()
		{
			var go = gameObject;
			var ac = go.GetComponent<TAuthoring>();
			if (ac == null && go.transform.parent != null) {
				ac = go.transform.parent.GetComponent<TAuthoring>();
			}

			if (ac == null && go.transform.parent.transform.parent != null) {
				ac = go.transform.parent.transform.parent.GetComponent<TAuthoring>();
			}

			if (ac == null) {
				Debug.LogWarning("No same- or parent authoring component found.");
			}

			return ac;
		}
	}
}
