// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;

namespace VisualPinball.Unity
{
	[DisableAutoCreation]
	internal class StaticNarrowPhaseSystem : SystemBase
	{
		public bool CollideAgainstPlayfieldPlane;
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
			var collideAgainstPlayfieldPlane = CollideAgainstPlayfieldPlane;
			var collEntity = _collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<ColliderData>(collEntity);
			var contacts = _simulateCycleSystemGroup.Contacts;
			var hitTime = _simulateCycleSystemGroup.HitTime;
			var random = new Random((uint)UnityEngine.Random.Range(1, 100000));
			var marker = PerfMarker;

			Entities
				.WithName("StaticNarrowPhaseJob")
				.ForEach((Entity ballEntity, ref CollisionEventData collEvent,
					ref DynamicBuffer<BallInsideOfBufferElement> insideOfs,
					in DynamicBuffer<OverlappingStaticColliderBufferElement> colliderIds, in BallData ballData) =>
				{

				// don't play with frozen balls
				if (ballData.IsFrozen) {
					return;
				}

				marker.Begin();

				// retrieve static data
				ref var colliders = ref collData.Value.Value.Colliders;

				// init contacts and event
				collEvent.ClearCollider(hitTime); // search upto current hit time

				// check playfield and glass first
				if (collideAgainstPlayfieldPlane) {
					ref var playfieldCollider = ref colliders[collData.Value.Value.PlayfieldColliderId].Value;
					HitTest(ref playfieldCollider, ref collEvent, ref contacts, ref insideOfs, in ballEntity, in ballData);
				}
				ref var glassCollider = ref colliders[collData.Value.Value.GlassColliderId].Value;
				HitTest(ref glassCollider, ref collEvent, ref contacts, ref insideOfs, in ballEntity, in ballData);

				var traversalOrder = false; //random.NextBool();
				var start = traversalOrder ? 0 : colliderIds.Length - 1;
				var end = traversalOrder ? colliderIds.Length : -1;
				var dt = traversalOrder ? 1 : -1;

				for (var i  = start; i != end; i += dt) {
					ref var coll = ref colliders[colliderIds[i].Value].Value;

					var newCollEvent = new CollisionEventData();
					float newTime = 0;
					unsafe {
						fixed (Collider* collider = &coll) {
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
									newTime = Collider.HitTest(ref coll, ref newCollEvent, ref insideOfs, in ballData, collEvent.HitTime);
									break;
							}
						}
					}

					SaveCollisions(ref collEvent, ref newCollEvent, ref contacts, in ballEntity, in coll, newTime);
				}

				// no negative time allowed
				if (collEvent.HitTime < 0) {
					collEvent.ClearCollider();
				}

				marker.End();

			}).Run();
		}

		private static void HitTest(ref Collider coll, ref CollisionEventData collEvent,
			ref NativeList<ContactBufferElement> contacts, ref DynamicBuffer<BallInsideOfBufferElement> insideOfs,
			in Entity ballEntity, in BallData ballData) {

			// todo
			// if (collider.obj && collider.obj.abortHitTest && collider.obj.abortHitTest()) {
			// 	return;
			// }

			var newCollEvent = new CollisionEventData();
			var newTime = Collider.HitTest(ref coll, ref newCollEvent, ref insideOfs, in ballData, collEvent.HitTime);

			SaveCollisions(ref collEvent, ref newCollEvent, ref contacts, in ballEntity, in coll, newTime);
		}

		private static void SaveCollisions(ref CollisionEventData collEvent, ref CollisionEventData newCollEvent,
			ref NativeList<ContactBufferElement> contacts, in Entity ballEntity, in Collider coll, float newTime)
		{
			var validHit = newTime >= 0f && !Math.Sign(newTime) && newTime <= collEvent.HitTime;

			if (newCollEvent.IsContact || validHit) {
				newCollEvent.SetCollider(coll.Id);
				newCollEvent.HitTime = newTime;
				if (newCollEvent.IsContact) {
					contacts.Add(new ContactBufferElement(ballEntity, newCollEvent));

				} else { // if (validhit)
					collEvent = newCollEvent;
				}
			}
		}
	}
}
