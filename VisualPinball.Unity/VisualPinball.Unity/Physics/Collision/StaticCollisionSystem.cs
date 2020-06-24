// ReSharper disable ConvertIfStatementToSwitchStatement

using System;
using Unity.Entities;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using VisualPinball.Unity.Physics.Collider;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;
using VisualPinball.Unity.VPT.Bumper;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Gate;
using VisualPinball.Unity.VPT.Plunger;
using VisualPinball.Unity.VPT.Spinner;
using VisualPinball.Unity.VPT.Trigger;
using Random = Unity.Mathematics.Random;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class StaticCollisionSystem : SystemBase
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private EntityQuery _collDataEntityQuery;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("StaticCollisionSystem");

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
			var random = new Random((uint)UnityEngine.Random.Range(1, 100000));

			var hitTime = _simulateCycleSystemGroup.HitTime;
			var marker = PerfMarker;

			Entities
				.WithName("StaticCollisionJob")
				.ForEach((ref BallData ballData, ref CollisionEventData collEvent,
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
					fixed (Collider.Collider* collider = &coll) {

						switch (coll.Type) {
							case ColliderType.Bumper:
								var bumperStaticData = GetComponent<BumperStaticData>(coll.Entity);
								var ringData = GetComponent<BumperRingAnimationData>(bumperStaticData.RingEntity);
								var skirtData = GetComponent<BumperSkirtAnimationData>(bumperStaticData.SkirtEntity);
								BumperCollider.Collide(ref ballData, ref collEvent, ref ringData, ref skirtData, in coll, bumperStaticData, ref random);
								SetComponent(bumperStaticData.RingEntity, ringData);
								SetComponent(bumperStaticData.SkirtEntity, skirtData);
								break;

							case ColliderType.Flipper:
								var flipperVelocityData = GetComponent<FlipperVelocityData>(coll.Entity);
								var flipperMovementData = GetComponent<FlipperMovementData>(coll.Entity);
								var flipperMaterialData = GetComponent<FlipperStaticData>(coll.Entity);

								((FlipperCollider*) collider)->Collide(
									ref ballData, ref collEvent, ref flipperMovementData,
									in flipperMaterialData, in flipperVelocityData
								);
								SetComponent(coll.Entity, flipperMovementData);
								break;

							case ColliderType.Gate:
								var gateMovementData = GetComponent<GateMovementData>(coll.Entity);
								var gateStaticData = GetComponent<GateStaticData>(coll.Entity);
								GateCollider.Collide(
									in ballData, ref collEvent, ref gateMovementData,
									in gateStaticData
								);
								SetComponent(coll.Entity, gateMovementData);
								break;

							case ColliderType.LineSlingShot:
								var slingshotData = GetComponent<LineSlingshotData>(coll.Entity);
								((LineSlingshotCollider*) collider)->Collide(
									ref ballData, in slingshotData,
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
									ref ballData, ref collEvent, ref insideOfs, ref triggerAnimationData, in coll
								);
								SetComponent(coll.Entity, triggerAnimationData);
								break;

							case ColliderType.Line:
							case ColliderType.Line3D:
							case ColliderType.Circle:
							case ColliderType.LineZ:
							case ColliderType.Plane:
							case ColliderType.Point:
							case ColliderType.Poly3D:
							case ColliderType.Triangle:
								Collider.Collider.Collide(ref coll, ref ballData, collEvent, ref random);
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
		}
	}
}
