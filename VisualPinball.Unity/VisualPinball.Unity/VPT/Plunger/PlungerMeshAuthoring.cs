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

using System.Collections.Generic;
using Unity.Entities;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	public abstract class PlungerMeshAuthoring : ItemMeshAuthoring<Plunger, PlungerData, PlungerAuthoring>, IConvertGameObjectToEntity
	{
		internal abstract void SetChildEntity(ref PlungerStaticData staticData, Entity entity);

		protected abstract IEnumerable<Vertex3DNoTex2> GetVertices(PlungerMeshGenerator meshGenerator, int frame);

		protected override bool IsVisible {
			get => Data.IsVisible;
			set => Data.IsVisible = value;
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var plunger = transform.parent.gameObject.GetComponent<PlungerAuthoring>().Item;
			var plungerEntity = new Entity {Index = plunger.Index, Version = plunger.Version};

			// update parent
			var plungerStaticData = dstManager.GetComponentData<PlungerStaticData>(plungerEntity);
			SetChildEntity(ref plungerStaticData, entity);
			dstManager.SetComponentData(plungerEntity, plungerStaticData);

			// add animation data
			dstManager.AddComponentData(entity, new PlungerAnimationData {
				CurrentFrame = 0
			});

			PostConvert(entity, dstManager, plunger.MeshGenerator);
		}

		protected virtual void PostConvert(Entity entity, EntityManager dstManager, PlungerMeshGenerator meshGenerator)
		{
		}
	}
}
