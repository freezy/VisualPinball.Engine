using NLog;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;
using VisualPinball.Unity.VPT.Flipper;
using Logger = NLog.Logger;
using Random = Unity.Mathematics.Random;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class ContactSystem : SystemBase
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

		protected override void OnUpdate()
		{
			var rnd = new Random(666);
			var hitTime = _simulateCycleSystemGroup.HitTime;
			var gravity = _gravity;

			// retrieve reference to static collider data
			var collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(ColliderData));
			var collEntity = collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<ColliderData>(collEntity);

			Entities.WithoutBurst().ForEach((ref BallData ballData, ref CollisionEventData collEvent,
				ref DynamicBuffer<ContactBufferElement> contacts) => {

				ref var colliders = ref collData.Value.Value.Colliders;

				//if (rnd.NextBool()) { // swap order of contact handling randomly
					// tslint:disable-next-line:prefer-for-of
				for (var i = 0; i < contacts.Length; i++) {
					var contact = contacts[i];
					ref var coll = ref colliders[contact.ColliderId].Value;
					unsafe {
						fixed (Collider.Collider* collider = &coll) {
							switch (coll.Type) {
								case ColliderType.Flipper:
									var flipperMovementData = GetComponent<FlipperMovementData>(coll.Entity);
									var flipperMaterialData = GetComponent<FlipperMaterialData>(coll.Entity);
									var flipperVelocityData = GetComponent<FlipperVelocityData>(coll.Entity);
									((FlipperCollider*) collider)->Contact(
										ref ballData, ref collEvent, ref flipperMovementData,
										in flipperMaterialData, in flipperVelocityData, hitTime, in gravity);
									break;

								default:
									Collider.Collider.Contact(ref coll, ref ballData, in contact.CollisionEvent, hitTime, in gravity);
									break;
							}
						}
					}
				}
				// } else {
				// 	for (var i = contacts.Length - 1; i != -1; --i) {
				// 		var contact = contacts[i];
				// 		ref var collider = ref colliders[contact.ColliderId].Value;
				// 		collider.Contact(ref ballData, in contact.CollisionEvent, hitTime, gravity);
				// 	}
				// }

			}).ScheduleParallel();
		}
	}
}
