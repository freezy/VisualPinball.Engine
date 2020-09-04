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

using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	public abstract class PlungerChildAuthoring : MonoBehaviour, IConvertGameObjectToEntity
	{
		internal abstract void SetChildEntity(ref PlungerStaticData staticData, Entity entity);

		protected abstract IEnumerable<Vertex3DNoTex2> GetVertices(PlungerMeshGenerator meshGenerator, int frame);

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;
			var plunger = transform.parent.gameObject.GetComponent<PlungerAuthoring>().Item;
			var plungerEntity = new Entity {Index = plunger.Index, Version = plunger.Version};
			plunger.MeshGenerator.Init(table);

			// update parent
			var plungerStaticData = dstManager.GetComponentData<PlungerStaticData>(plungerEntity);
			SetChildEntity(ref plungerStaticData, entity);
			dstManager.SetComponentData(plungerEntity, plungerStaticData);

			// add animation data
			dstManager.AddComponentData(entity, new PlungerAnimationData {
				CurrentFrame = 0
			});

			// add mesh data
			var meshBuffer = dstManager.AddBuffer<PlungerMeshBufferElement>(entity);
			for (var frame = 0; frame < plunger.MeshGenerator.NumFrames; frame++) {
				var vertices = GetVertices(plunger.MeshGenerator, frame);
				foreach (var v in vertices) {
					meshBuffer.Add(new PlungerMeshBufferElement(new float3(v.X, v.Y, v.Z)));
				}
			}

			PostConvert(entity, dstManager, plunger.MeshGenerator);
		}

		protected virtual void PostConvert(Entity entity, EntityManager dstManager, PlungerMeshGenerator meshGenerator)
		{
		}
	}
}
