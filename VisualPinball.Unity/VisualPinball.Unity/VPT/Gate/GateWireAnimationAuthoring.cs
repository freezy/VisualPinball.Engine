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

using Unity.Entities;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity
{
	public class GateWireAnimationAuthoring : ItemMovementAuthoring<Gate, GateData, GateAuthoring>, IConvertGameObjectToEntity
	{
		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var gateEntity = Entity;

			// update parent
			var gateStaticData = dstManager.GetComponentData<GateStaticData>(gateEntity);
			gateStaticData.WireEntity = entity;
			dstManager.SetComponentData(gateEntity, gateStaticData);

			// add movement data
			dstManager.AddComponentData(entity, new GateMovementData {
				Angle = Data.AngleMin,
				AngleSpeed = 0,
				ForcedMove = false,
				IsOpen = false
			});

			LinkToParentEntity(entity, dstManager);
		}
	}
}
