using Unity.Entities;
using UnityEngine;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.VPT.Bumper
{
	public class BumperRingBehavior : MonoBehaviour, IConvertGameObjectToEntity
	{
		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var table = gameObject.GetComponentInParent<TableBehavior>().Item;
			var bumper = transform.parent.gameObject.GetComponent<BumperBehavior>().Item;
			var bumperEntity = new Entity {Index = bumper.Index, Version = bumper.Version};

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
		}
	}
}
