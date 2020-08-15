// ReSharper disable ConvertIfStatementToSwitchStatement

using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Engine.VPT;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace VisualPinball.Unity
{
	[DisableAutoCreation]
	public class StaticCollisionSystem : SystemBase
	{
		private Player _player;
		private VisualPinballSimulationSystemGroup _visualPinballSimulationSystemGroup;
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private EntityQuery _collDataEntityQuery;
		private NativeQueue<EventData> _eventQueue;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("StaticCollisionSystem");

		protected override void OnCreate()
		{
			_visualPinballSimulationSystemGroup = World.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
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
							case ColliderType.Bumper:
								var bumperStaticData = GetComponent<BumperStaticData>(coll.Entity);
								var ringData = GetComponent<BumperRingAnimationData>(bumperStaticData.RingEntity);
								var skirtData = GetComponent<BumperSkirtAnimationData>(bumperStaticData.SkirtEntity);
								BumperCollider.Collide(ref ballData, ref events, ref collEvent, ref ringData, ref skirtData, in coll, bumperStaticData, ref random);
								SetComponent(bumperStaticData.RingEntity, ringData);
								SetComponent(bumperStaticData.SkirtEntity, skirtData);
								break;

							case ColliderType.Flipper:
								var flipperVelocityData = GetComponent<FlipperVelocityData>(coll.Entity);
								var flipperMovementData = GetComponent<FlipperMovementData>(coll.Entity);
								var flipperMaterialData = GetComponent<FlipperStaticData>(coll.Entity);
								var flipperHitData = GetComponent<FlipperHitData>(coll.Entity);

								((FlipperCollider*) collider)->Collide(
									ref ballData, ref collEvent, ref flipperMovementData, ref events,
									in flipperMaterialData, in flipperVelocityData, in flipperHitData, timeMsec
								);
								SetComponent(coll.Entity, flipperMovementData);
								break;

							case ColliderType.Gate:
								var gateMovementData = GetComponent<GateMovementData>(coll.Entity);
								var gateStaticData = GetComponent<GateStaticData>(coll.Entity);
								GateCollider.Collide(
									ref ballData, ref collEvent, ref gateMovementData, ref events,
									in coll, in gateStaticData
								);
								SetComponent(coll.Entity, gateMovementData);
								break;

							case ColliderType.LineSlingShot:
								var slingshotData = GetComponent<LineSlingshotData>(coll.Entity);
								((LineSlingshotCollider*) collider)->Collide(
									ref ballData, ref events, in slingshotData,
									in collEvent, ref random);
								break;

							case ColliderType.Plunger:
								var plungerMovementData = GetComponent<PlungerMovementData>(coll.Entity);
								var plungerStaticData = GetComponent<PlungerStaticData>(coll.Entity);
								PlungerCollider.Collide(
									ref ballData, ref collEvent, ref plungerMovementData,
									in plungerStaticData, ref random);
								SetComponent(coll.Entity, plungerMovementData);
								break;

							case ColliderType.Spinner:
								var spinnerMovementData = GetComponent<SpinnerMovementData>(coll.Entity);
								var spinnerStaticData = GetComponent<SpinnerStaticData>(coll.Entity);
								SpinnerCollider.Collide(
									in ballData, ref collEvent, ref spinnerMovementData,
									in spinnerStaticData
								);
								SetComponent(coll.Entity, spinnerMovementData);
								break;

							case ColliderType.TriggerCircle:
							case ColliderType.TriggerLine:
								var triggerAnimationData = GetComponent<TriggerAnimationData>(coll.Entity);
								TriggerCollider.Collide(
									ref ballData, ref events, ref collEvent, ref insideOfs, ref triggerAnimationData, in coll
								);
								SetComponent(coll.Entity, triggerAnimationData);
								break;

							case ColliderType.KickerCircle:
								var kickerCollisionData = GetComponent<KickerCollisionData>(coll.Entity);
								var kickerStaticData = GetComponent<KickerStaticData>(coll.Entity);
								// ReSharper disable once ConditionIsAlwaysTrueOrFalse
								var legacyMode = KickerCollider.ForceLegacyMode || kickerStaticData.LegacyMode;
								var kickerMeshData = !legacyMode ? GetComponent<ColliderMeshData>(coll.Entity) : default;
								KickerCollider.Collide(ref ballData, ref events, ref insideOfs, ref kickerCollisionData,
									in kickerStaticData, in kickerMeshData, in collEvent, coll.Entity, in ballEntity, false
								);
								SetComponent(coll.Entity, kickerCollisionData);
								break;

							case ColliderType.Line:
							case ColliderType.Line3D:
							case ColliderType.Circle:
							case ColliderType.LineZ:
							case ColliderType.Plane:
							case ColliderType.Point:
							case ColliderType.Poly3D:
							case ColliderType.Triangle:

								// hit target
								if (coll.Header.ItemType == ItemType.HitTarget) {

									float3 normal;
									if (coll.Type == ColliderType.Poly3D) {
										normal = ((Poly3DCollider*) collider)->Normal();

									} else if (coll.Type == ColliderType.Triangle) {
										normal = ((TriangleCollider*) collider)->Normal();

									} else {
										normal = collEvent.HitNormal;
									}

									var hitTargetAnimationData = GetComponent<HitTargetAnimationData>(coll.Entity);
									HitTargetCollider.Collide(ref ballData, ref events, ref hitTargetAnimationData,
										in normal, in collEvent, in coll, ref random);
									SetComponent(coll.Entity, hitTargetAnimationData);

								// trigger
								} else if (coll.Header.ItemType == ItemType.Trigger) {
									TriggerCollider. Collide(ref ballData, ref events, ref collEvent, ref insideOfs, in coll);

								} else {
									Collider.Collide(ref coll, ref ballData, ref events, in collEvent, ref random);
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
