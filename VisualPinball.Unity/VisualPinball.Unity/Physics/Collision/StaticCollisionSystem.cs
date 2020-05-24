// ReSharper disable ConvertIfStatementToSwitchStatement

using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Profiling;
using VisualPinball.Unity.Physics.Collider;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;
using VisualPinball.Unity.VPT.Flipper;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class StaticCollisionSystem : SystemBase
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
			var random = new Random((uint)UnityEngine.Random.Range(1, 100000));

			var hitTime = _simulateCycleSystemGroup.HitTime;

			Entities.WithName("StaticCollisionJob").ForEach((ref BallData ballData, ref CollisionEventData collEvent) => {

				// find balls with hit objects and minimum time
				if (collEvent.ColliderId < 0 || collEvent.HitTime > hitTime) {
					return;
				}

				// Profiler.BeginSample("StaticCollisionSystem");

				// retrieve static data
				ref var colliders = ref collData.Value.Value.Colliders;

				// pick collider that matched during narrowphase
				ref var coll = ref colliders[collEvent.ColliderId].Value; // object that ball hit in trials

				// now collision, contact and script reactions on active ball (object)+++++++++

				//this.activeBall = ball;                         // For script that wants the ball doing the collision

				unsafe {
					fixed (Collider.Collider* collider = &coll) {

						if (coll.Type == ColliderType.Flipper) {
							var flipperVelocityData = GetComponent<FlipperVelocityData>(coll.Entity);
							var flipperMovementData = GetComponent<FlipperMovementData>(coll.Entity);
							var flipperMaterialData = GetComponent<FlipperStaticData>(coll.Entity);
							((FlipperCollider*) collider)->Collide(
								ref ballData, ref collEvent, ref flipperMovementData,
								in flipperMaterialData, in flipperVelocityData
							);
							SetComponent(coll.Entity, flipperMovementData);

						} else if (coll.Type == ColliderType.LineSlingShot) {
							//Debug.Log("Entering slingshot with type = " + coll.Type + " and entity = " + coll.Entity);
							var slingshotData = GetComponent<LineSlingshotData>(coll.Entity);
							((LineSlingshotCollider*) collider)->Collide(ref ballData, in slingshotData, in collEvent, ref random);

						} else {
							Collider.Collider.Collide(ref coll, ref ballData, collEvent, ref random);
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

				// Profiler.EndSample();

			}).Run();
		}
	}
}
