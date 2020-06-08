﻿using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.Bumper
{
	public class BumperSkirtBehavior : MonoBehaviour, IConvertGameObjectToEntity
	{
		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var bumper = transform.parent.gameObject.GetComponent<BumperBehavior>().Item;
			var bumperEntity = new Entity {Index = bumper.Index, Version = bumper.Version};

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
		}
	}
}
