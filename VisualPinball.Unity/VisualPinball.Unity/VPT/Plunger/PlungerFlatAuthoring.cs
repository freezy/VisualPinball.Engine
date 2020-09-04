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
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity
{
	public class PlungerFlatAuthoring : PlungerChildAuthoring
	{
		internal override void SetChildEntity(ref PlungerStaticData staticData, Entity entity)
		{
			staticData.FlatEntity = entity;
		}

		protected override IEnumerable<Vertex3DNoTex2> GetVertices(PlungerMeshGenerator meshGenerator, int frame)
		{
			return meshGenerator.BuildFlatVertices(frame);
		}

		protected override void PostConvert(Entity entity, EntityManager dstManager, PlungerMeshGenerator meshGenerator)
		{
			// add mesh data
			var uvBuffer = dstManager.AddBuffer<PlungerUvBufferElement>(entity);
			for (var frame = 0; frame < meshGenerator.NumFrames; frame++) {
				var vertices = meshGenerator.BuildFlatVertices(frame);
				foreach (var v in vertices) {
					uvBuffer.Add(new PlungerUvBufferElement(new float2(v.Tu, v.Tv)));
				}
			}
		}
	}
}
