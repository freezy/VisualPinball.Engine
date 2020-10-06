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
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Playfield;

namespace VisualPinball.Unity
{
	public class PlayfieldAuthoring : ItemMainAuthoring<Table, TableData>,
		IHittableAuthoring, IMeshAuthoring, IConvertGameObjectToEntity
	{
		public IRenderable Renderable => Table;

		public IHittable Hittable => Table;

		protected override Table InstantiateItem(TableData data) => throw new InvalidOperationException("Table is not instantiated via authoring component.");

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;
			table.Index = entity.Index;
			table.Version = entity.Version;
		}

		public void RemoveHittableComponent()
		{
			var rc = gameObject.GetComponent<PlayfieldColliderAuthoring>();
			if (rc != null) {
				DestroyImmediate(rc);
			}
		}

		public void RemoveMeshComponent()
		{
			var rc = gameObject.GetComponent<PlayfieldMeshAuthoring>();
			if (rc != null) {
				DestroyImmediate(rc);
			}
		}
	}
}
