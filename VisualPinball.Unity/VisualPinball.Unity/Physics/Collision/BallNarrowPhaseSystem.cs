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
			// retrieve reference to static collider data
			var collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(ColliderData));
			var collEntity = collDataEntityQuery.GetSingletonEntity();
			var collData = EntityManager.GetComponentData<ColliderData>(collEntity);

			var hitTime = (float)_simulateCycleSystemGroup.DTime;

			return Entities.WithoutBurst().ForEach((ref DynamicBuffer<MatchedColliderBufferElement> matchedColliderIds, ref CollisionEventData collEvent,
				ref DynamicBuffer<ContactBufferElement> contacts, in BallData ballData) => {

				// retrieve static data
				ref var colliders = ref collData.Value.Value.Colliders;
				ref var playfieldCollider = ref colliders[collData.Value.Value.PlayfieldColliderId].Value;
				ref var glassCollider = ref colliders[collData.Value.Value.GlassColliderId].Value;

				// init contacts and event
				var validColl = Collider.Collider.None;
				contacts.Clear();
				collEvent.HitTime = hitTime; // search upto current hittime

				// check playfield and glass first
				HitTest(ref playfieldCollider, ref validColl, ref collEvent, ref contacts, ballData);
				HitTest(ref glassCollider, ref validColl, ref collEvent, ref contacts, ballData);

				for (var i = 0; i < matchedColliderIds.Length; i++) {
					ref var coll = ref colliders[matchedColliderIds[i].Value].Value;
					HitTest(ref coll, ref validColl, ref collEvent, ref contacts, ballData);
				}

				// don't need those anymore
				matchedColliderIds.Clear();

				// todo probably faster to add it as separate data via a EntityCommandBufferSystem
				if (validColl.Type != ColliderType.None) {
					matchedColliderIds.Add(new MatchedColliderBufferElement {Value = validColl.Id});
				}

			}).Schedule(inputDeps);
		}
		private static void HitTest(ref Collider.Collider coll, ref Collider.Collider validColl,
			ref CollisionEventData collEvent, ref DynamicBuffer<ContactBufferElement> contacts, in BallData ballData) {

			// todo
			// if (collider.obj && collider.obj.abortHitTest && collider.obj.abortHitTest()) {
			// 	return;
			// }

			var newCollEvent = new CollisionEventData();
			var newTime = Collider.Collider.HitTest(ref coll, ref newCollEvent, in ballData, collEvent.HitTime);
			var validHit = newTime >= 0 && newTime <= collEvent.HitTime;

			if (newCollEvent.IsContact || validHit) {
				if (newCollEvent.IsContact) {
					contacts.Add(new ContactBufferElement {
						CollisionEvent = newCollEvent,
						ColliderId = coll.Id
					});

				} else {                         // if (validhit)
					collEvent.Set(newCollEvent);
					collEvent.HitTime = newTime;
					validColl = coll;
				}
			}
		}
	}

}
