using Unity.Entities;
using Unity.Jobs;
using VisualPinball.Unity.Physics.SystemGroup;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[UpdateInGroup(typeof(BallNarrowPhaseSystemGroup))]
	public class BallNarrowPhaseSystem : JobComponentSystem
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var hitTime = (float)_simulateCycleSystemGroup.DTime;
			return Entities.WithoutBurst().ForEach((ref DynamicBuffer<ColliderBufferElement> colliders, ref CollisionEventData collEvent,
				ref DynamicBuffer<ContactBufferElement> contacts, in BallData ballData) => {

				var validColl = Collider.Collider.None;
				contacts.Clear();
				collEvent.HitTime = hitTime; // search upto current hittime
				for (var i = 0; i < colliders.Length; i++) {
					var coll = colliders[i].Value;

					// todo
					// if (collider.obj && collider.obj.abortHitTest && collider.obj.abortHitTest()) {
					// 	return;
					// }

					var newCollEvent = new CollisionEventData();
					var newTime = coll.HitTest(ref newCollEvent, in ballData, collEvent.HitTime);
					var validHit = newTime >= 0 && newTime <= collEvent.HitTime;

					if (newCollEvent.IsContact || validHit) {
						if (newCollEvent.IsContact) {
							contacts.Add(new ContactBufferElement {
								CollisionEvent = newCollEvent,
								Collider = coll
							});

						} else {                         // if (validhit)
							collEvent.Set(newCollEvent);
							collEvent.HitTime = newTime;
							validColl = coll;
						}
					}
				}

				// don't need those anymore
				colliders.Clear();

				// todo probably faster to add it as separate data via a EntityCommandBufferSystem
				if (validColl.Type != ColliderType.None) {
					colliders.Add(new ColliderBufferElement {Value = validColl});
				}

			}).Schedule(inputDeps);
		}
	}
}
