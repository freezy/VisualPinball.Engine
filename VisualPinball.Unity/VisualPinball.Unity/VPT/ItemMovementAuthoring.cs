﻿// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using Unity.Transforms;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public abstract class ItemMovementAuthoring<TItem, TData, TMainAuthoring> : ItemSubAuthoring<TItem, TData, TMainAuthoring>,
		IItemMovementAuthoring
		where TData : ItemData
		where TItem : Item<TData>, IRenderable
		where TMainAuthoring : ItemMainRenderableAuthoring<TItem, TData>
	{
		protected Entity MainEntity {
			get {
				var ma = MainAuthoring;
				if (ma == null) {
					throw new InvalidOperationException("Cannot find main authoring component of " + name + ".");
				}
				var item = ma.Item;
				return new Entity { Index = item.Index, Version = item.Version };
			}
		}

		protected void LinkToParentEntity(Entity entity, EntityManager dstManager)
		{
			dstManager.AddComponentData(entity, new Parent {Value = MainEntity});
			dstManager.AddComponentData(entity, new LocalToParent());
		}
	}
}
