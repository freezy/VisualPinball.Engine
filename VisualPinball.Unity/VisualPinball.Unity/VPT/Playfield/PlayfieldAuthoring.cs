// Visual Pinball Engine
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
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Playfield;

namespace VisualPinball.Unity
{
	public class PlayfieldAuthoring : ItemMainRenderableAuthoring<Table, TableData>,
		IConvertGameObjectToEntity
	{
		public override bool CanBeTransformed => false;

		protected override Table InstantiateItem(TableData data) => GetComponentInParent<TableAuthoring>()?.Table;

		protected override Type MeshAuthoringType { get; } = null;
		protected override Type ColliderAuthoringType { get; } = null;

		public override IEnumerable<Type> ValidParents => PlayfieldColliderAuthoring.ValidParentTypes
			.Concat(PlayfieldMeshAuthoring.ValidParentTypes)
			.Distinct();

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;
			table.Index = entity.Index;
			table.Version = entity.Version;
		}

		public override void SetData(TableData data, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
		}

		public override void CopyDataTo(TableData data)
		{
		}
	}
}
