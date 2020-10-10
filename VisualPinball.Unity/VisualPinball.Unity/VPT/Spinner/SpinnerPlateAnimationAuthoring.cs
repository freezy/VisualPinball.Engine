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
using Unity.Mathematics;
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity
{
	public class SpinnerPlateAnimationAuthoring : ItemMovementAuthoring<Spinner, SpinnerData, SpinnerAuthoring>, IConvertGameObjectToEntity
	{
		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var spinnerEntity = Entity;

			// update parent
			var spinnerStaticData = dstManager.GetComponentData<SpinnerStaticData>(spinnerEntity);
			spinnerStaticData.PlateEntity = entity;
			dstManager.SetComponentData(spinnerEntity, spinnerStaticData);

			dstManager.AddComponentData(entity, new SpinnerMovementData {
				Angle = math.radians(math.clamp(0.0f, Data.AngleMin, Data.AngleMax)),
				AngleSpeed = 0f
			});

			LinkToParentEntity(entity, dstManager);
		}
	}
}
