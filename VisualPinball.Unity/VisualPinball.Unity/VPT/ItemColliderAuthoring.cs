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
	public abstract class ItemColliderAuthoring<TItem, TData, TAuthoring> : ItemAuthoring<TItem, TData>
		where TData : ItemData
		where TItem : Item<TData>, IHittable, IRenderable
		where TAuthoring : ItemAuthoring<TItem, TData>
	{
		public TData Data => GetData();

		protected TData _data;

		public IItemAuthoring SetItem(TItem item, RenderObjectGroup rog)
		{
			_data = item.Data;
			name = rog.ComponentName + " (collider)";
			return this;
		}

		protected override string[] Children => new string[0];

		private TData GetData()
		{
			// if data is set, this is a full-fledged item
			if (_data != null) {
				return _data;
			}

			// otherwise, retrieve data from parent
			var go = gameObject;
			var ac = go.GetComponent<TAuthoring>();
			if (ac == null && go.transform.parent != null) {
				ac = go.transform.parent.GetComponent<TAuthoring>();
			}

			if (ac == null && go.transform.parent.transform.parent != null) {
				ac = go.transform.parent.transform.parent.GetComponent<TAuthoring>();
			}

			if (ac != null) {
				return ac.data;
			}

			Debug.LogWarning("No same- or parent authoring component found.");
			return null;
		}
	}
}
