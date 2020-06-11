using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.VPT.Plunger
{
	public class PlungerSpringBehavior : MonoBehaviour, IConvertGameObjectToEntity
	{
		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			var table = gameObject.GetComponentInParent<TableBehavior>().Item;
			var plunger = transform.parent.gameObject.GetComponent<PlungerBehavior>().Item;
			var plungerEntity = new Entity {Index = plunger.Index, Version = plunger.Version};
			plunger.MeshGenerator.Init(table);

			// update parent
			var plungerStaticData = dstManager.GetComponentData<PlungerStaticData>(plungerEntity);
			plungerStaticData.SpringEntity = entity;
			dstManager.SetComponentData(plungerEntity, plungerStaticData);

			// add animation data
			dstManager.AddComponentData(entity, new PlungerAnimationData {
				CurrentFrame = 0
			});

			// add mesh data
			var meshBuffer = dstManager.AddBuffer<PlungerMeshBufferElement>(entity);
			for (var frame = 0; frame < plunger.MeshGenerator.NumFrames; frame++) {
				var vertices = plunger.MeshGenerator.BuildSpringVertices(frame);
				foreach (var v in vertices) {
					meshBuffer.Add(new PlungerMeshBufferElement(new float3(v.X, v.Y, v.Z)));
				}
			}
		}
	}
}
