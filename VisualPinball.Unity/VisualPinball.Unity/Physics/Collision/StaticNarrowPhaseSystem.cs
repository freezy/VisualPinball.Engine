﻿using Unity.Entities;
 using Unity.Profiling;
 using VisualPinball.Unity.Physics.Collider;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;
using VisualPinball.Unity.VPT.Flipper;
 using VisualPinball.Unity.VPT.Plunger;

 namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class StaticNarrowPhaseSystem : SystemBase
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private EntityQuery _collDataEntityQuery;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("StaticNarrowPhaseSystem");

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
			_collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(ColliderData));
		}

		protected override void OnUpdate()
		{
			// retrieve reference to static collider data

			var collEntity = _collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<ColliderData>(collEntity);

			var hitTime = _simulateCycleSystemGroup.HitTime;
			var marker = PerfMarker;

			Entities.WithName("DynamicNarrowPhaseJob").ForEach((ref CollisionEventData collEvent,
				ref DynamicBuffer<ContactBufferElement> contacts, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs,
				in DynamicBuffer<OverlappingStaticColliderBufferElement> colliderIds, in BallData ballData) => {

				marker.Begin();

				// don't play with frozen balls
				if (ballData.IsFrozen) {
					return;
				}

				// retrieve static data
				ref var colliders = ref collData.Value.Value.Colliders;
				ref var playfieldCollider = ref colliders[collData.Value.Value.PlayfieldColliderId].Value;
				ref var glassCollider = ref colliders[collData.Value.Value.GlassColliderId].Value;

				// init contacts and event
				contacts.Clear();
				collEvent.ClearCollider(hitTime); // search upto current hittime

				// check playfield and glass first
				HitTest(ref playfieldCollider, ref collEvent, ref contacts, ref insideOfs, in ballData);
				HitTest(ref glassCollider, ref collEvent, ref contacts, ref insideOfs, in ballData);

				// statics first (todo: randomly switch order)
				for (var i = 0; i < colliderIds.Length; i++) {
					ref var coll = ref colliders[colliderIds[i].Value].Value;

					var newCollEvent = new CollisionEventData();
					float newTime = 0;
					unsafe {
						fixed (Collider.Collider* collider = &coll) {
							switch (coll.Type) {

								case ColliderType.LineSlingShot:
									newTime = ((LineSlingshotCollider*) collider)->HitTest(ref newCollEvent, ref insideOfs, in ballData, collEvent.HitTime);
									break;

								case ColliderType.Flipper:
									if (HasComponent<FlipperHitData>(coll.Entity) &&
											HasComponent<FlipperMovementData>(coll.Entity) &&
											HasComponent<FlipperStaticData>(coll.Entity))
										{
											var flipperHitData = GetComponent<FlipperHitData>(coll.Entity);
											var flipperMovementData = GetComponent<FlipperMovementData>(coll.Entity);
											var flipperMaterialData = GetComponent<FlipperStaticData>(coll.Entity);
											newTime = ((FlipperCollider*)collider)->HitTest(
												ref newCollEvent, ref insideOfs, ref flipperHitData,
												in flipperMovementData, in flipperMaterialData, in ballData, collEvent.HitTime
											);

											SetComponent(coll.Entity, flipperHitData);
										}
									break;

								case ColliderType.Plunger:
									if (HasComponent<PlungerColliderData>(coll.Entity) &&
									    HasComponent<PlungerStaticData>(coll.Entity) &&
									    HasComponent<PlungerMovementData>(coll.Entity))
									{
										var plungerColliderData = GetComponent<PlungerColliderData>(coll.Entity);
										var plungerStaticData = GetComponent<PlungerStaticData>(coll.Entity);
										var plungerMovementData = GetComponent<PlungerMovementData>(coll.Entity);
										newTime = ((PlungerCollider*)collider)->HitTest(
											ref newCollEvent, ref insideOfs, ref plungerMovementData,
											in plungerColliderData, in plungerStaticData, in ballData, collEvent.HitTime
										);

										SetComponent(coll.Entity, plungerMovementData);
									}
									break;

								default:
									newTime = Collider.Collider.HitTest(ref coll, ref newCollEvent, ref insideOfs, in ballData, collEvent.HitTime);
									break;
							}
						}
					}

					SaveCollisions(ref collEvent, ref newCollEvent, ref contacts, in coll, newTime);
				}

				// no negative time allowed
				if (collEvent.HitTime < 0) {
					collEvent.ClearCollider();
				}

				marker.End();

			}).Run();
		}

		private static void HitTest(ref Collider.Collider coll, ref CollisionEventData collEvent,
			ref DynamicBuffer<ContactBufferElement> contacts, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs,
			in BallData ballData) {

			// todo
			// if (collider.obj && collider.obj.abortHitTest && collider.obj.abortHitTest()) {
			// 	return;
			// }

			var newCollEvent = new CollisionEventData();
			var newTime = Collider.Collider.HitTest(ref coll, ref newCollEvent, ref insideOfs, in ballData, collEvent.HitTime);

			SaveCollisions(ref collEvent, ref newCollEvent, ref contacts, in coll, newTime);
		}

		private static void SaveCollisions(ref CollisionEventData collEvent, ref CollisionEventData newCollEvent,
			ref DynamicBuffer<ContactBufferElement> contacts, in Collider.Collider coll, float newTime)
		{
			var validHit = newTime >= 0 && newTime <= collEvent.HitTime;

			if (newCollEvent.IsContact || validHit) {
				newCollEvent.SetCollider(coll.Id);
				newCollEvent.HitTime = newTime;
				if (newCollEvent.IsContact) {
					contacts.Add(new ContactBufferElement(coll.Id, newCollEvent));

				} else {                         // if (validhit)
					collEvent = newCollEvent;
				}
			}
		}
	}
}
