using NLog;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
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
		private EntityQuery _collDataEntityQuery;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
			_collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(ColliderData));
		}

		protected override void OnStartRunning()
		{
			_gravity = Object.FindObjectOfType<Player>().GetGravity();
		}

		protected override void OnUpdate()
		{
			// Profiler.BeginSample("ContactSystem");

			var rnd = new Random(666);
			var hitTime = _simulateCycleSystemGroup.HitTime;
			var gravity = _gravity;

			// retrieve reference to static collider data
			var collEntity = _collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<ColliderData>(collEntity);

			Entities.WithName("ContactJob").ForEach((ref BallData ball, ref CollisionEventData collEvent,
				ref DynamicBuffer<ContactBufferElement> contacts) => {

				ref var colliders = ref collData.Value.Value.Colliders;

				//if (rnd.NextBool()) { // swap order of contact handling randomly
					// tslint:disable-next-line:prefer-for-of
				for (var i = 0; i < contacts.Length; i++) {
					var contact = contacts[i];
					if (contact.ColliderId > -1) {
						ref var coll = ref colliders[contact.ColliderId].Value;
						unsafe {
							fixed (Collider.Collider* collider = &coll) {
								switch (coll.Type) {
									case ColliderType.Flipper:
										var flipperMovementData = GetComponent<FlipperMovementData>(coll.Entity);
										var flipperMaterialData = GetComponent<FlipperStaticData>(coll.Entity);
										var flipperVelocityData = GetComponent<FlipperVelocityData>(coll.Entity);
										((FlipperCollider*) collider)->Contact(
											ref ball, ref collEvent, ref flipperMovementData,
											in flipperMaterialData, in flipperVelocityData, hitTime, in gravity);
										SetComponent(coll.Entity, flipperMovementData);
										break;

									default:
										Collider.Collider.Contact(ref coll, ref ball, in contact.CollisionEvent, hitTime, in gravity);
										break;
								}
							}
						}
					} else if (contact.ColliderEntity != Entity.Null) {
						// todo move ball friction into some data component
						BallCollider.HandleStaticContact(ref ball, collEvent, 0.3f, hitTime, gravity);
					}
				}

				contacts.Clear();

			}).Run();

			// Profiler.EndSample();
		}
	}
}
