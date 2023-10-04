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

// ReSharper disable ConvertIfStatementToSwitchStatement

using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity;
using VisualPinball.Unity.VisualPinball.Unity.Game;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace VisualPinballUnity
{
	[DisableAutoCreation]
	internal partial class StaticCollisionSystem : SystemBaseStub
	{
		private Player _player;
		private VisualPinballSimulationSystemGroup _visualPinballSimulationSystemGroup;
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private EntityQuery _collDataEntityQuery;
		private NativeQueue<EventData> _eventQueue;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("StaticCollisionSystem");

		protected override void OnCreate()
		{
			_visualPinballSimulationSystemGroup = World.GetOrCreateSystemManaged<VisualPinballSimulationSystemGroup>();
			_simulateCycleSystemGroup = World.GetOrCreateSystemManaged<SimulateCycleSystemGroup>();
			_collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(ColliderData));
			_eventQueue = new NativeQueue<EventData>(Allocator.Persistent);
		}

		protected override void OnStartRunning()
		{
			_player = Object.FindObjectOfType<Player>();
		}

		protected override void OnDestroy()
		{
			_eventQueue.Dispose();
		}

		protected override void OnUpdate()
		{
			// retrieve reference to static collider data
			var collEntity = _collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<ColliderData>(collEntity);
			var random = new global::Unity.Mathematics.Random((uint)Random.Range(1, 100000));

			var events = _eventQueue.AsParallelWriter();

			var hitTime = _simulateCycleSystemGroup.HitTime;
			var timeMsec = _visualPinballSimulationSystemGroup.TimeMsec;
			var marker = PerfMarker;

			Entities
				.WithName("StaticCollisionJob")
				.ForEach((Entity ballEntity, ref BallData ballData, ref CollisionEventData collEvent,
					ref DynamicBuffer<BallInsideOfBufferElement> insideOfs) => {

				var ballId = 0;
				// find balls with hit objects and minimum time
				if (collEvent.ColliderId < 0 || collEvent.HitTime > hitTime) {
					return;
				}

				marker.Begin();

				// retrieve static data
				ref var colliders = ref collData.Value.Value.Colliders;

				// pick collider that matched during narrowphase
				ref var coll = ref colliders[collEvent.ColliderId].Value; // object that ball hit in trials

				// now collision, contact and script reactions on active ball (object)+++++++++

				//this.activeBall = ball;                         // For script that wants the ball doing the collision

				unsafe {
					fixed (Collider* collider = &coll) {

						switch (coll.Type) {
							case ColliderType.Bumper: {
								var bumperStaticData = GetComponent<BumperStaticData>(coll.ItemId);
								var animateRing = HasComponent<BumperRingAnimationData>(coll.ItemId);
								var animateSkirt = HasComponent<BumperSkirtAnimationData>(coll.ItemId);
								var ringData = animateRing ? GetComponent<BumperRingAnimationData>(coll.ItemId) : default;
								var skirtData = animateSkirt ? GetComponent<BumperSkirtAnimationData>(coll.ItemId): default;
								BumperCollider.Collide(ref ballData, ref events, ref collEvent, ref ringData, ref skirtData,
									in coll, bumperStaticData, ref random);
								if (animateRing) {
									SetComponent(coll.ItemId, ringData);
								}
								if (animateSkirt) {
									SetComponent(coll.ItemId, skirtData);
								}
								break;
							}

							case ColliderType.Flipper: {
								var flipperVelocityData = GetComponent<FlipperVelocityData>(coll.ItemId);
								var flipperMovementData = GetComponent<FlipperMovementData>(coll.ItemId);
								var flipperMaterialData = GetComponent<FlipperStaticData>(coll.ItemId);
								var flipperHitData = GetComponent<FlipperHitData>(coll.ItemId);
								var flipperTricksData = GetComponent<FlipperTricksData>(coll.ItemId);
								// do liveCatch - check before collision
								FlipperCollider.LiveCatch(
									ref ballData, ref collEvent, ref flipperTricksData, in flipperMaterialData, timeMsec
								);
								((FlipperCollider*)collider)->Collide(
									ref ballData, ref collEvent, ref flipperMovementData, ref events,
									in ballId, in flipperTricksData,in flipperMaterialData, in flipperVelocityData, in flipperHitData, timeMsec
								);
								SetComponent(coll.ItemId, flipperMovementData);
								break;
							}

							case ColliderType.Gate: {
								var gateStaticData = GetComponent<GateStaticData>(coll.ItemId);
								var gateMovementData = GetComponent<GateMovementData>(coll.ItemId);
								GateCollider.Collide(
									ref ballData, ref collEvent, ref gateMovementData, ref events,
									in ballId, in coll, in gateStaticData
								);
								SetComponent(coll.ItemId, gateMovementData);
								break;
							}

							case ColliderType.LineSlingShot: {
								var slingshotData = GetComponent<LineSlingshotData>(coll.ItemId);
								((LineSlingshotCollider*)collider)->Collide(
									ref ballData, ref events,
									in ballId, in slingshotData, in collEvent, ref random);
								break;
							}

							case ColliderType.Plunger: {
								var plungerMovementData = GetComponent<PlungerMovementData>(coll.ItemId);
								var plungerStaticData = GetComponent<PlungerStaticData>(coll.ItemId);
								PlungerCollider.Collide(
									ref ballData, ref collEvent, ref plungerMovementData,
									in plungerStaticData, ref random);
								SetComponent(coll.ItemId, plungerMovementData);
								break;
							}

							case ColliderType.Spinner: {
								var spinnerStaticData = GetComponent<SpinnerStaticData>(coll.ItemId);
								var spinnerMovementData = GetComponent<SpinnerMovementData>(coll.ItemId);
								SpinnerCollider.Collide(
									in ballData, ref collEvent, ref spinnerMovementData,
									in spinnerStaticData
								);
								SetComponent(coll.ItemId, spinnerMovementData);
								break;
							}

							case ColliderType.TriggerCircle:
							case ColliderType.TriggerLine: {

								var triggerAnimationData = HasComponent<TriggerAnimationData>(coll.ItemId)
									? GetComponent<TriggerAnimationData>(coll.ItemId)
									: new TriggerAnimationData();

								// TriggerCollider.Collide(
								// 	ref ballData, ref events, ref collEvent, ref insideOfs, ref triggerAnimationData,
								// 	in ballId, in coll
								// );

								if (HasComponent<FlipperCorrectionData>(coll.ItemId)) {
									if (triggerAnimationData.UnHitEvent) {
										var flipperCorrectionData = GetComponent<FlipperCorrectionData>(coll.ItemId);
										ref var flipperCorrectionBlob = ref flipperCorrectionData.Value.Value;
										var flipperMovementData = GetComponent<FlipperMovementData>(flipperCorrectionBlob.FlipperEntity);
										var flipperStaticData = GetComponent<FlipperStaticData>(flipperCorrectionBlob.FlipperEntity);
										var flipperTricksData = GetComponent<FlipperTricksData>(flipperCorrectionBlob.FlipperEntity);
										FlipperCorrection.OnBallLeaveFlipper(
											ref ballData, ref flipperCorrectionBlob, in flipperMovementData, in flipperTricksData, in flipperStaticData, timeMsec
										);
									}

								} else {
									SetComponent(coll.ItemId, triggerAnimationData);
								}
								break;
							}

							case ColliderType.KickerCircle: {
								var kickerCollisionData = GetComponent<KickerCollisionData>(coll.ItemId);
								var kickerStaticData = GetComponent<KickerStaticData>(coll.ItemId);
								// ReSharper disable once ConditionIsAlwaysTrueOrFalse
								var legacyMode = KickerCollider.ForceLegacyMode || kickerStaticData.LegacyMode;
								// ReSharper disable once ConditionIsAlwaysTrueOrFalse
								var kickerMeshData = !legacyMode ? GetComponent<ColliderMeshData>(coll.ItemId) : default;
								// KickerCollider.Collide(ref ballData, ref events, ref insideOfs, ref kickerCollisionData,
								// 	in kickerStaticData, in kickerMeshData, in collEvent, coll.ItemId, in ballId
								// );
								SetComponent(coll.ItemId, kickerCollisionData);
								break;
							}

							case ColliderType.Line:
							case ColliderType.Line3D:
							case ColliderType.Circle:
							case ColliderType.LineZ:
							case ColliderType.Plane:
							case ColliderType.Point:
							case ColliderType.Triangle:

								// hit target
								if (coll.Header.ItemType == ItemType.HitTarget) {

									var normal = coll.Type == ColliderType.Triangle
										? ((TriangleCollider*) collider)->Normal()
										: collEvent.HitNormal;

									if (HasComponent<DropTargetAnimationData>(coll.ItemId)) {
										var dropTargetAnimationData = GetComponent<DropTargetAnimationData>(coll.ItemId);
										TargetCollider.DropTargetCollide(ref ballData, ref events, ref dropTargetAnimationData,
											in normal, in ballId, in collEvent, in coll, ref random);
										SetComponent(coll.ItemId, dropTargetAnimationData);
									}

									if (HasComponent<HitTargetAnimationData>(coll.ItemId)) {
										var hitTargetAnimationData = GetComponent<HitTargetAnimationData>(coll.ItemId);
										TargetCollider.HitTargetCollide(ref ballData, ref events, ref hitTargetAnimationData,
											in normal, in ballId, in collEvent, in coll, ref random);
										SetComponent(coll.ItemId, hitTargetAnimationData);
									}

								// trigger
								} else if (coll.Header.ItemType == ItemType.Trigger) {

									var triggerAnimationData = HasComponent<TriggerAnimationData>(coll.ItemId)
										? GetComponent<TriggerAnimationData>(coll.ItemId)
										: new TriggerAnimationData();

									// TriggerCollider.Collide(
									// 	ref ballData, ref events, ref collEvent, ref insideOfs, ref triggerAnimationData,
									// 	in ballId, in coll
									// );

									if (HasComponent<FlipperCorrectionData>(coll.ItemId)) {
										if (triggerAnimationData.UnHitEvent) {
											var flipperCorrectionData = GetComponent<FlipperCorrectionData>(coll.ItemId);
											ref var flipperCorrectionBlob = ref flipperCorrectionData.Value.Value;
											var flipperMovementData = GetComponent<FlipperMovementData>(flipperCorrectionBlob.FlipperEntity);
											var flipperStaticData = GetComponent<FlipperStaticData>(flipperCorrectionBlob.FlipperEntity);
											var flipperTricksData = GetComponent<FlipperTricksData>(flipperCorrectionBlob.FlipperEntity);
											FlipperCorrection.OnBallLeaveFlipper(
												ref ballData, ref flipperCorrectionBlob, in flipperMovementData, in flipperTricksData, in flipperStaticData, timeMsec
											);
										}

									} else {
										SetComponent(coll.ItemId, triggerAnimationData);
									}

								} else {
									Collider.Collide(in coll, ref ballData, ref events, in ballId, in collEvent, ref random);
								}
								break;

							case ColliderType.None:
							default:
								throw new ArgumentOutOfRangeException();
						}
					}
				}

				// remove trial hit object pointer
				collEvent.ClearCollider();

				// todo fix below (probably just delete)
				// Collide may have changed the velocity of the ball,
				// and therefore the bounding box for the next hit cycle
				// if (this.balls[i] !== ball) { // Ball still exists? may have been deleted from list
				//
				// 	// collision script deleted the ball, back up one count
				// 	--i;
				//
				// } else {
				// 	ball.hit.calcHitBBox(); // do new boundings
				// }

				marker.End();

			}).Run();

			while (_eventQueue.TryDequeue(out var eventData)) {
				_player.OnEvent(eventData);
			}
		}
	}
}
