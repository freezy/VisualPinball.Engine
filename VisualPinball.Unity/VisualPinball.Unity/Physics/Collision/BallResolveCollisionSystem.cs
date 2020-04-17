using Unity.Entities;
using Unity.Jobs;
using UnityEngine;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class BallResolveCollisionSystem : JobComponentSystem
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var hitTime = _simulateCycleSystemGroup.DTime;
			return Entities.ForEach((ref BallData ballData, ref DynamicBuffer<ColliderBufferElement> colliders, ref CollisionEventData collEvent) => {

				if (colliders.Length == 0) {
					return;
				}

				if (colliders.Length != 1) {
					Debug.LogWarning($"Number of colliders should be 1 by now, but it's {colliders.Length}.");
					return;
				}

				var pho = colliders[0].Value; // object that ball hit in trials

				// find balls with hit objects and minimum time
				if (collEvent.HitTime <= hitTime) {
					// now collision, contact and script reactions on active ball (object)+++++++++

					//this.activeBall = ball;                         // For script that wants the ball doing the collision
					pho.Collide(ref ballData, collEvent);          // !!!!! 3) collision on active ball

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

			}).Schedule(inputDeps);
		}
	}
}
