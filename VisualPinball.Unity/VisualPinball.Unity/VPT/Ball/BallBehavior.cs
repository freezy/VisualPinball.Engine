using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Physics.Collision;

namespace VisualPinball.Unity.VPT.Ball
{
	public class BallBehavior : MonoBehaviour, IConvertGameObjectToEntity
	{
		public float3 Position;
		public float3 Velocity;
		public float Radius;
		public float Mass;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			dstManager.AddComponentData(entity, new BallData {
				IsFrozen = false,
				Position = Position,
				Radius = Radius,
				Mass = Mass,
				Velocity = Velocity,
				Orientation = float3x3.identity
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
			dstManager.AddBuffer<MatchedColliderBufferElement>(entity);
			dstManager.AddBuffer<MatchedBallColliderBufferElement>(entity);
			dstManager.AddBuffer<ContactBufferElement>(entity);
			dstManager.AddBuffer<BallInsideOfBufferElement>(entity);
		}
	}
}
