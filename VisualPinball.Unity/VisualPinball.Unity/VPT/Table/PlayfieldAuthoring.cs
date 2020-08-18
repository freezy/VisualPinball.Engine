using Unity.Entities;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class PlayfieldAuthoring : MonoBehaviour, IConvertGameObjectToEntity
	{
		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;
			table.Index = entity.Index;
			table.Version = entity.Version;
		}
	}
}
