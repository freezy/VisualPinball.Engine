using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[UpdateInGroup(typeof(BallNarrowPhaseSystemGroup))]
	public class BallNarrowPhaseSystem : JobComponentSystem
	{
		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			return Entities.ForEach((ref DynamicBuffer<ColliderBufferElement> colliders, ref CollisionEventData collEvent,
				ref DynamicBuffer<ContactBufferElement> contacts, in BallData ballData) => {

				for (var i = 0; i < colliders.Length; i++) {
					var coll = colliders[i].Value;

					// todo
					// if (collider.obj && collider.obj.abortHitTest && collider.obj.abortHitTest()) {
					// 	return;
					// }

					var newCollEvent = new CollisionEventData();
					var newTime = coll.HitTest(ballData, collEvent.hitTime, newCollEvent);
					var validHit = newTime >= 0 && newTime <= collEvent.hitTime;

					if (newCollEvent.isContact || validHit) {
						if (newCollEvent.isContact) {
							contacts.Add(new ContactBufferElement { Value = newCollEvent });

						} else {                         // if (validhit)
							collEvent.Set(newCollEvent);
							collEvent.hitTime = newTime;
						}
					}
				}

				// don't need those anymore
				colliders.Clear();

			}).Schedule(inputDeps);
		}
	}
}
