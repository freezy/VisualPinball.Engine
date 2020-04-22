using Unity.Entities;
using UnityEngine;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;
using VisualPinball.Unity.VPT.Flipper;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class BallResolveCollisionSystem : SystemBase
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

			Entities.WithoutBurst().ForEach((ref BallData ballData,
				ref DynamicBuffer<MatchedColliderBufferElement> matchedColliderIds, ref CollisionEventData collEvent) => {

				if (matchedColliderIds.Length == 0) {
					return;
				}

				if (matchedColliderIds.Length != 1) {
					Debug.LogWarning($"Number of matched colliders should be 1 by now, but it's {matchedColliderIds.Length}.");
					return;
				}

				// retrieve static data
				ref var colliders = ref collData.Value.Value.Colliders;

				// pick collider that matched during narrowphase
				ref var coll = ref colliders[matchedColliderIds[0].Value].Value; // object that ball hit in trials

				// find balls with hit objects and minimum time
				if (collEvent.HitTime <= hitTime) {
					// now collision, contact and script reactions on active ball (object)+++++++++

					//this.activeBall = ball;                         // For script that wants the ball doing the collision

					unsafe {
						fixed (Collider.Collider* collider = &coll) {
							switch (coll.Type) {
								case ColliderType.Flipper:
									var flipperVelocityData = GetComponent<FlipperVelocityData>(coll.Entity);
									var flipperMovementData = GetComponent<FlipperMovementData>(coll.Entity);
									var flipperMaterialData = GetComponent<FlipperMaterialData>(coll.Entity);
									((FlipperCollider*) collider)->Collide(
										ref ballData, ref collEvent, ref flipperMovementData,
										in flipperMaterialData, in flipperVelocityData
									);
									break;

								default:
									Collider.Collider.Collide(ref coll, ref ballData, collEvent);          // !!!!! 3) collision on active ball
									break;
							}
						}
					}

					// todo fix below
					// ball.coll.clear();                     // remove trial hit object pointer
					//
					// // Collide may have changed the velocity of the ball,
					// // and therefore the bounding box for the next hit cycle
					// if (this.balls[i] !== ball) { // Ball still exists? may have been deleted from list
					//
					// 	// collision script deleted the ball, back up one count
					// 	--i;
					//
					// } else {
					// 	ball.hit.calcHitBBox(); // do new boundings
					// }
				}

				matchedColliderIds.Clear();

			}).ScheduleParallel();
		}
	}
}
