using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

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
		}
	}
}
