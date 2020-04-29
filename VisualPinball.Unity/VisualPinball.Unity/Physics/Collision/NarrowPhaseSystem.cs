﻿using Unity.Entities;
using VisualPinball.Unity.Physics.Collider;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;
using VisualPinball.Unity.VPT.Flipper;

 namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class NarrowPhaseSystem : SystemBase
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override void OnUpdate()
		{
			// retrieve reference to static collider data
			var collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(ColliderData));
			var collEntity = collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<ColliderData>(collEntity);

			var hitTime = _simulateCycleSystemGroup.HitTime;

			Entities.WithName("NarrowPhaseJob").ForEach((ref DynamicBuffer<OverlappingStaticColliderBufferElement> staticColliderIds,
				ref DynamicBuffer<OverlappingDynamicBufferElement> dynamicEntities, ref CollisionEventData collEvent,
				ref DynamicBuffer<ContactBufferElement> contacts, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs,
				in BallData ballData) => {

				// retrieve static data
				ref var colliders = ref collData.Value.Value.Colliders;
				ref var playfieldCollider = ref colliders[collData.Value.Value.PlayfieldColliderId].Value;
				ref var glassCollider = ref colliders[collData.Value.Value.GlassColliderId].Value;

				// init contacts and event
				var validColl = Collider.Collider.None;
				contacts.Clear();
				collEvent.Reset(hitTime); // search upto current hittime

				// check playfield and glass first
				HitTest(ref playfieldCollider, ref collEvent, ref validColl, ref contacts, ref insideOfs, in ballData);
				HitTest(ref glassCollider, ref collEvent, ref validColl, ref contacts, ref insideOfs, in ballData);

				// statics first (todo: randomly switch order)
				for (var i = 0; i < staticColliderIds.Length; i++) {
					ref var coll = ref colliders[staticColliderIds[i].Value].Value;

					var newCollEvent = new CollisionEventData();
					float newTime;
					unsafe {
						fixed (Collider.Collider* collider = &coll) {
							switch (coll.Type) {

								case ColliderType.LineSlingShot:
									newTime = ((LineSlingshotCollider*) collider)->HitTest(ref newCollEvent, in ballData, collEvent.HitTime);
									break;

								case ColliderType.Flipper:
									var flipperHitData = GetComponent<FlipperHitData>(coll.Entity);
									var flipperMovementData = GetComponent<FlipperMovementData>(coll.Entity);
									var flipperMaterialData = GetComponent<FlipperStaticData>(coll.Entity);
									newTime = ((FlipperCollider*) collider)->HitTest(
										ref newCollEvent, ref insideOfs, ref flipperHitData,
										in flipperMovementData, in flipperMaterialData, in ballData, collEvent.HitTime
									);
									break;

								default:
									newTime = Collider.Collider.HitTest(ref coll, ref newCollEvent, ref insideOfs, in ballData, collEvent.HitTime);
									break;
							}
						}
					}

					SaveCollisions(ref collEvent, ref newCollEvent, ref contacts, in coll, newTime, ref validColl);
				}

				// secondly, dynamic checks
				var validBallEntity = Entity.Null;
				for (var i = 0; i < dynamicEntities.Length; i++) {
					var collBallEntity = dynamicEntities[i].Value;
					var collBall = GetComponent<BallData>(collBallEntity);
					var newCollEvent = new CollisionEventData();
					var newTime = BallCollider.HitTest(ref newCollEvent, ref collBall, in ballData, collEvent.HitTime);

					SaveCollisions(ref collEvent, ref newCollEvent, ref contacts, in collBallEntity, newTime, ref validBallEntity);
				}

				staticColliderIds.Clear();
				dynamicEntities.Clear();

				// if (collEvent.HitTime >= 0) {
				// 	if (validBallEntity != Entity.Null) {
				// 		dynamicEntities.Add(new OverlappingDynamicBufferElement { Value = validBallEntity });
				//
				// 	} else if (validColl.Type != ColliderType.None) {
				// 		staticColliderIds.Add(new OverlappingStaticColliderBufferElement { Value = validColl.Id });
				// 	}
				// }

			}).Run();
		}

		private static void HitTest(ref Collider.Collider coll, ref CollisionEventData collEvent,
			ref Collider.Collider validColl,
			ref DynamicBuffer<ContactBufferElement> contacts, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs,
			in BallData ballData) {

			// todo
			// if (collider.obj && collider.obj.abortHitTest && collider.obj.abortHitTest()) {
			// 	return;
			// }

			var newCollEvent = new CollisionEventData();
			var newTime = Collider.Collider.HitTest(ref coll, ref newCollEvent, ref insideOfs, in ballData, collEvent.HitTime);

			SaveCollisions(ref collEvent, ref newCollEvent, ref contacts, in coll, newTime, ref validColl);
		}

		private static void SaveCollisions(ref CollisionEventData collEvent, ref CollisionEventData newCollEvent,
			ref DynamicBuffer<ContactBufferElement> contacts, in Collider.Collider coll, float newTime,
			ref Collider.Collider validColl)
		{
			var validHit = newTime >= 0 && newTime <= collEvent.HitTime;

			if (newCollEvent.IsContact || validHit) {
				newCollEvent.HitTime = newTime;
				if (newCollEvent.IsContact) {
					contacts.Add(new ContactBufferElement {
						CollisionEvent = newCollEvent,
						ColliderId = coll.Id
					});

				} else {                         // if (validhit)
					collEvent.Set(coll.Id, newCollEvent);
					validColl = coll;
				}
			}
		}

		private static void SaveCollisions(ref CollisionEventData collEvent, ref CollisionEventData newCollEvent,
			ref DynamicBuffer<ContactBufferElement> contacts, in Entity ballEntity, float newTime,
			ref Entity validBallEntity)
		{
			var validHit = newTime >= 0 && newTime <= collEvent.HitTime;

			if (newCollEvent.IsContact || validHit) {
				newCollEvent.HitTime = newTime;
				if (newCollEvent.IsContact) {
					contacts.Add(new ContactBufferElement {
						CollisionEvent = newCollEvent,
						ColliderEntity = ballEntity
					});

				} else {                         // if (validhit)
					collEvent.Set(ballEntity, newCollEvent);
					validBallEntity = ballEntity;
				}
			}
		}
	}
}
