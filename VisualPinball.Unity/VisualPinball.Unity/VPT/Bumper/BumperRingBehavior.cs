using Unity.Entities;
using UnityEngine;

namespace VisualPinball.Unity.VPT.Bumper
{
	public class BumperRingBehavior : MonoBehaviour, IConvertGameObjectToEntity
	{
		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var bumper = transform.parent.gameObject.GetComponent<BumperBehavior>().Item;
			var bumperEntity = new Entity {Index = bumper.Index, Version = bumper.Version};

			// update parent
			var bumperStaticData = dstManager.GetComponentData<BumperStaticData>(bumperEntity);
			bumperStaticData.RingEntity = entity;
			dstManager.SetComponentData(bumperEntity, bumperStaticData);

			// add ring data
			dstManager.AddComponentData(entity, new BumperRingAnimationData {
				IsHit = false
			});
		}
	}
}
