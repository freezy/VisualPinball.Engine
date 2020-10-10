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
using UnityEngine;
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Animation/Bumper Skirt Animation")]
	public class BumperSkirtAnimationAuthoring : ItemMovementAuthoring<Bumper, BumperData, BumperAuthoring>, IConvertGameObjectToEntity
	{
		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var bumper = Item;
			var bumperEntity = Entity;

			// update parent
			var bumperStaticData = dstManager.GetComponentData<BumperStaticData>(bumperEntity);
			bumperStaticData.SkirtEntity = entity;
			dstManager.SetComponentData(bumperEntity, bumperStaticData);

			// add ring data
			dstManager.AddComponentData(entity, new BumperSkirtAnimationData {
				BallPosition = default,
				AnimationCounter = 0f,
				DoAnimate = false,
				DoUpdate = false,
				EnableAnimation = true,
				Rotation = new float2(0, 0),
				HitEvent = bumper.Data.HitEvent,
				Center = bumper.Data.Center.ToUnityFloat2()
			});

			LinkToParentEntity(entity, dstManager);
		}
	}
}
