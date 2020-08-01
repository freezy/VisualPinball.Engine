using Unity.Entities;
using UnityEngine;

namespace VisualPinball.Unity.VPT.Table
{
	public class PlayfieldBehavior : MonoBehaviour, IConvertGameObjectToEntity
	{
		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var table = gameObject.GetComponentInParent<TableBehavior>().Item;
			table.Index = entity.Index;
			table.Version = entity.Version;
		}
	}
}
