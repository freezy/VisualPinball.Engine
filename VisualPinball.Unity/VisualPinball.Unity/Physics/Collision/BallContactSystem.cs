using NLog;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;
using Logger = NLog.Logger;
using Random = Unity.Mathematics.Random;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class BallContactSystem : JobComponentSystem
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private float3 _gravity;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override void OnStartRunning()
		{
			_gravity = Object.FindObjectOfType<Player>().GetGravity();
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var rnd = new Random(666);
			var hitTime = _simulateCycleSystemGroup.HitTime;
			var gravity = _gravity;

			// retrieve reference to static collider data
			var collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(ColliderData));
			var collEntity = collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<ColliderData>(collEntity);

			return Entities.WithoutBurst().ForEach((ref BallData ballData, ref DynamicBuffer<ContactBufferElement> contacts) => {

				ref var colliders = ref collData.Value.Value.Colliders;

				if (rnd.NextBool()) { // swap order of contact handling randomly
					// tslint:disable-next-line:prefer-for-of
					for (var i = 0; i < contacts.Length; i++) {
						var contact = contacts[i];
						ref var collider = ref colliders[contact.ColliderId].Value;
						collider.Contact(ref ballData, in contact.CollisionEvent, hitTime, gravity);
					}
				} else {
					for (var i = contacts.Length - 1; i != -1; --i) {
						var contact = contacts[i];
						ref var collider = ref colliders[contact.ColliderId].Value;
						collider.Contact(ref ballData, in contact.CollisionEvent, hitTime, gravity);
					}
				}

			}).Schedule(inputDeps);
		}
	}
}
