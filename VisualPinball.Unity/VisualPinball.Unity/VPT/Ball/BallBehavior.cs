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
				Mass = Mass,
				Velocity = new float3(0, 0, 0)
			});
			dstManager.AddComponentData(entity, new CollisionEventData {
				HitTime = -1,
				IsContact = false
			});
			// dstManager.AddComponentData(entity, new VisualPinball.Unity.Physics.Collider.Collider {
			// 	Header = new ColliderHeader {
			// 		Type = ColliderType.None,
			// 		Aabb = new Aabb(),
			// 		EntityIndex = -1
			// 	}
			// });
			dstManager.AddBuffer<ColliderBufferElement>(entity);
			dstManager.AddBuffer<ContactBufferElement>(entity);
		}
	}
}
