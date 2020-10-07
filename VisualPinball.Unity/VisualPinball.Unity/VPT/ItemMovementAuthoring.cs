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
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Spinner;
using Color = UnityEngine.Color;

namespace VisualPinball.Unity
{
	public abstract class ItemMovementAuthoring<TItem, TData, TMainAuthoring> : ItemSubAuthoring<TItem, TData, TMainAuthoring>,
		IItemColliderAuthoring
		where TData : ItemData
		where TItem : Item<TData>, IHittable, IRenderable
		where TMainAuthoring : ItemMainAuthoring<TItem, TData>
	{
		protected Entity Entity {
			get {
				var ma = MainAuthoring;
				if (ma == null) {
					throw new InvalidOperationException("Cannot find main authoring component of " + name + ".");
				}
				var item = ma.Item;
				return new Entity { Index = item.Index, Version = item.Version };
			}
		}
	}
}
