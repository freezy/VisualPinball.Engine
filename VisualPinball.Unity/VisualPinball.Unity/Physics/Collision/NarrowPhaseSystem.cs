﻿using Unity.Entities;
 using UnityEngine.Profiling;
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

				Profiler.BeginSample("NarrowPhaseSystem");

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

					SaveCollisions(ref collEvent, ref newCollEvent, ref contacts, in coll, newTime);
				}

				// secondly, dynamic checks
				for (var i = 0; i < dynamicEntities.Length; i++) {
					var collBallEntity = dynamicEntities[i].Value;
					var collBall = GetComponent<BallData>(collBallEntity);
					var newCollEvent = new CollisionEventData();
					var newTime = BallCollider.HitTest(ref newCollEvent, ref collBall, in ballData, collEvent.HitTime);

					SaveCollisions(ref collEvent, ref newCollEvent, ref contacts, in collBallEntity, newTime);
				}

				staticColliderIds.Clear();
				dynamicEntities.Clear();

				// no negative time allowed
				if (collEvent.HitTime < 0) {
					collEvent.ClearCollider();
				}

				Profiler.EndSample();

			}).ScheduleParallel();
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

		private static void SaveCollisions(ref CollisionEventData collEvent, ref CollisionEventData newCollEvent,
			ref DynamicBuffer<ContactBufferElement> contacts, in Entity ballEntity, float newTime)
		{
			var validHit = newTime >= 0 && newTime <= collEvent.HitTime;

			if (newCollEvent.IsContact || validHit) {
				newCollEvent.SetCollider(ballEntity);
				newCollEvent.HitTime = newTime;
				if (newCollEvent.IsContact) {
					contacts.Add(new ContactBufferElement(ballEntity, newCollEvent));

				} else {                         // if (validhit)
					collEvent = newCollEvent;
				}
			}
		}
	}
}
