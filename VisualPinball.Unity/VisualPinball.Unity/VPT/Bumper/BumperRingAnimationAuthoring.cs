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
using UnityEngine;
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Animation/Bumper Ring Animation")]
	public class BumperRingAnimationAuthoring : ItemMovementAuthoring<Bumper, BumperData, BumperAuthoring>, IConvertGameObjectToEntity
	{
		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var table = Table;
			var bumper = Item;
			var bumperEntity = Entity;

			// update parent
			var bumperStaticData = dstManager.GetComponentData<BumperStaticData>(bumperEntity);
			bumperStaticData.RingEntity = entity;
			dstManager.SetComponentData(bumperEntity, bumperStaticData);

			// add ring data
			dstManager.AddComponentData(entity, new BumperRingAnimationData {

				// dynamic
				IsHit = false,
				Offset = 0,
				AnimateDown = false,
				DoAnimate = false,

				// static
				DropOffset = bumper.Data.RingDropOffset,
				HeightScale = bumper.Data.HeightScale,
				Speed = bumper.Data.RingSpeed,
				ScaleZ = table.GetScaleZ()
			});

			LinkToParentEntity(entity, dstManager);
		}
	}
}
