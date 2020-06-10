using Unity.Entities;
using UnityEngine;

namespace VisualPinball.Unity.VPT.Plunger
{
	public class PlungerRodBehavior : MonoBehaviour, IConvertGameObjectToEntity
	{
		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var plunger = transform.parent.gameObject.GetComponent<PlungerBehavior>().Item;
			var plungerEntity = new Entity {Index = plunger.Index, Version = plunger.Version};

			// update parent
			var plungerStaticData = dstManager.GetComponentData<PlungerStaticData>(plungerEntity);
			plungerStaticData.RodEntity = entity;
			dstManager.SetComponentData(plungerEntity, plungerStaticData);

			// add animation data
			dstManager.AddComponentData(entity, new PlungerAnimationData {
				CurrentFrame = 0
			});

			// add mesh data
			var meshBuffer = dstManager.AddBuffer<PlungerMeshBufferElement>(entity);

		}
	}
}
