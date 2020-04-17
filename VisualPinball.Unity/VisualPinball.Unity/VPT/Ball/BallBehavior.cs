using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Physics.Collision;

namespace VisualPinball.Unity.VPT.Ball
{
	public class BallBehavior : MonoBehaviour, IConvertGameObjectToEntity
	{
		public float3 Position;
		public float Radius;
		public float Mass;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			dstManager.AddComponentData(entity, new BallData {
				IsFrozen = false,
				Position = Position,
				Radius = Radius,
				Velocity = new float3(0, 0, 0)
			});
			dstManager.AddComponentData(entity, new CollisionEventData {
				hitTime = -1,
				isContact = false
			});
			dstManager.AddBuffer<ColliderBufferElement>(entity);
			dstManager.AddBuffer<ContactBufferElement>(entity);
		}
	}
}
