// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using VisualPinball.Engine.VPT;
using VisualPinball.Unity;
using VisualPinball.Unity.VisualPinball.Unity.Game;

namespace VisualPinballUnity
{
	[DisableAutoCreation]
	internal partial class StaticNarrowPhaseSystem : SystemBaseStub
	{
		public bool CollideAgainstPlayfieldPlane;
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private EntityQuery _collDataEntityQuery;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("StaticNarrowPhaseSystem");

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystemManaged<SimulateCycleSystemGroup>();
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
					HitTest(ref playfieldCollider, ref collEvent, ref contacts, ref insideOfs, in ballData.Id, in ballData);
				}
				ref var glassCollider = ref colliders[collData.Value.Value.GlassColliderId].Value;
				HitTest(ref glassCollider, ref collEvent, ref contacts, ref insideOfs, in ballData.Id, in ballData);

				var traversalOrder = false; //random.NextBool();
				var start = traversalOrder ? 0 : colliderIds.Length - 1;
				var end = traversalOrder ? colliderIds.Length : -1;
				var dt = traversalOrder ? 1 : -1;


				for (var i  = start; i != end; i += dt) {
					ref var coll = ref colliders[colliderIds[i].Value].Value;
					var saveCollision = true;

					var newCollEvent = new CollisionEventData();
					float newTime = 0;
					unsafe {
						fixed (Collider* collider = &coll) {
							switch (coll.Type) {

								case ColliderType.LineSlingShot:
//									newTime = ((LineSlingshotCollider*) collider)->HitTest(ref newCollEvent, ref insideOfs, in ballData, collEvent.HitTime);
									break;

								case ColliderType.Flipper:
									if (HasComponent<FlipperHitData>(coll.ItemId) &&
									    HasComponent<FlipperMovementData>(coll.ItemId) &&
									    HasComponent<FlipperStaticData>(coll.ItemId))
									{
										var flipperHitData = GetComponent<FlipperHitData>(coll.ItemId);
										var flipperMovementData = GetComponent<FlipperMovementData>(coll.ItemId);
										var flipperMaterialData = GetComponent<FlipperStaticData>(coll.ItemId);
										var flipperTricksData = GetComponent<FlipperTricksData>(coll.ItemId);
										// newTime = ((FlipperCollider*)collider)->HitTest(
										// 	ref newCollEvent, ref insideOfs, ref flipperHitData,
										// 	in flipperMovementData, in flipperTricksData, in flipperMaterialData, in ballData, collEvent.HitTime
										// );

										SetComponent(coll.ItemId, flipperHitData);
									}
									break;

								case ColliderType.Plunger:
									if (HasComponent<PlungerColliderData>(coll.ItemId) &&
									    HasComponent<PlungerStaticData>(coll.ItemId) &&
									    HasComponent<PlungerMovementData>(coll.ItemId))
									{
										var plungerColliderData = GetComponent<PlungerColliderData>(coll.ItemId);
										var plungerStaticData = GetComponent<PlungerStaticData>(coll.ItemId);
										var plungerMovementData = GetComponent<PlungerMovementData>(coll.ItemId);
										// newTime = ((PlungerCollider*)collider)->HitTest(
										// 	ref newCollEvent, ref insideOfs, ref plungerMovementData,
										// 	in plungerColliderData, in plungerStaticData, in ballData, collEvent.HitTime
										// );

										SetComponent(coll.ItemId, plungerMovementData);
									}
									break;
								case ColliderType.Line:
								case ColliderType.Line3D:
								case ColliderType.Circle:
								case ColliderType.LineZ:
								case ColliderType.Plane:
								case ColliderType.Point:
								case ColliderType.Triangle:
									// hit target
									if (coll.Header.ItemType == ItemType.HitTarget) {
										if (HasComponent<DropTargetAnimationData>(coll.ItemId)) {
											var dropTargetAnimationData = GetComponent<DropTargetAnimationData>(coll.ItemId);
											if (dropTargetAnimationData.IsDropped || dropTargetAnimationData.MoveAnimation) {  // QUICKFIX so that DT is not triggered twice
												saveCollision = false;
											}
											else {
												newTime = Collider.HitTest(ref coll, ref newCollEvent, ref insideOfs, in ballData, collEvent.HitTime);
											}
										}
										if (HasComponent<HitTargetAnimationData>(coll.ItemId)) {
											newTime = Collider.HitTest(ref coll, ref newCollEvent, ref insideOfs, in ballData, collEvent.HitTime);
										}
									}
									else
										newTime = Collider.HitTest(ref coll, ref newCollEvent, ref insideOfs, in ballData, collEvent.HitTime);
									break;

								default:
									newTime = Collider.HitTest(ref coll, ref newCollEvent, ref insideOfs, in ballData, collEvent.HitTime);
								break;
							}
						}
					}
					if (saveCollision) {
						SaveCollisions(ref collEvent, ref newCollEvent, ref contacts, in ballData.Id, in coll, newTime);
					}
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
			in int ballId, in BallData ballData) {

			// todo
			// if (collider.obj && collider.obj.abortHitTest && collider.obj.abortHitTest()) {
			// 	return;
			// }

			var newCollEvent = new CollisionEventData();
			var newTime = Collider.HitTest(ref coll, ref newCollEvent, ref insideOfs, in ballData, collEvent.HitTime);

			SaveCollisions(ref collEvent, ref newCollEvent, ref contacts, in ballId, in coll, newTime);
		}

		private static void SaveCollisions(ref CollisionEventData collEvent, ref CollisionEventData newCollEvent,
			ref NativeList<ContactBufferElement> contacts, in int ballId, in Collider coll, float newTime)
		{
			var validHit = newTime >= 0f && !Math.Sign(newTime) && newTime <= collEvent.HitTime;

			if (newCollEvent.IsContact || validHit) {
				newCollEvent.SetCollider(coll.Id);
				newCollEvent.HitTime = newTime;
				if (newCollEvent.IsContact) {
					contacts.Add(new ContactBufferElement(ballId, newCollEvent));

				} else { // if (validhit)
					collEvent = newCollEvent;
				}
			}
		}
	}
}
