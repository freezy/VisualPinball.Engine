// ReSharper disable ClassNeverInstantiated.Global

using NLog;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[DisableAutoCreation]
	public class ContactSystem : SystemBase
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private float3 _gravity;
		private EntityQuery _collDataEntityQuery;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("ContactSystem");

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

			var hitTime = _simulateCycleSystemGroup.HitTime;
			var gravity = _gravity;

			// retrieve reference to static collider data
			var collEntity = _collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<ColliderData>(collEntity);
			var marker = PerfMarker;

			Entities.WithName("ContactJob").ForEach((ref BallData ball, ref CollisionEventData collEvent,
				ref DynamicBuffer<ContactBufferElement> contacts) => {

				marker.Begin();
				ref var colliders = ref collData.Value.Value.Colliders;

				//if (rnd.NextBool()) { // swap order of contact handling randomly
				for (var i = 0; i < contacts.Length; i++) {
					var contact = contacts[i];
					if (contact.ColliderId > -1) {
						ref var coll = ref colliders[contact.ColliderId].Value;
						unsafe {
							fixed (Collider* collider = &coll) {
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
										Collider.Contact(ref coll, ref ball, in contact.CollisionEvent, hitTime, in gravity);
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
				marker.End();

			}).Run();
		}
	}
}
