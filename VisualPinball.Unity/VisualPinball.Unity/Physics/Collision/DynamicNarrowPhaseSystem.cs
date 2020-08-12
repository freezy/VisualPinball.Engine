using Unity.Entities;
using Unity.Profiling;
using VisualPinball.Unity.VPT.Ball;

namespace VisualPinball.Unity.Physics.Collision
{
	[DisableAutoCreation]
	public class DynamicNarrowPhaseSystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("DynamicNarrowPhaseSystem");

		protected override void OnUpdate()
		{
			var balls = GetComponentDataFromEntity<BallData>();
			var overlappingEntitiesBuffer = GetBufferFromEntity<OverlappingDynamicBufferElement>(true);
			var contactsBuffer = GetBufferFromEntity<ContactBufferElement>();
			var marker = PerfMarker;

			Entities
				.WithName("DynamicNarrowPhaseJob")
				.WithNativeDisableParallelForRestriction(balls)
				.WithNativeDisableParallelForRestriction(contactsBuffer)
				.WithReadOnly(overlappingEntitiesBuffer)
				.ForEach((Entity entity, ref BallData ball, ref CollisionEventData collEvent) => {

					marker.Begin();

					// don't play with frozen balls
					if (ball.IsFrozen) {
						return;
					}

					var contacts = contactsBuffer[entity];
					var overlappingEntities = overlappingEntitiesBuffer[entity];
					for (var k = 0; k < overlappingEntities.Length; k++) {
						var collBallEntity = overlappingEntities[k].Value;
						var collBall = balls[collBallEntity];

						var newCollEvent = new CollisionEventData();
						var newTime = BallCollider.HitTest(ref newCollEvent, ref collBall, in ball, collEvent.HitTime);

						SaveCollisions(ref collEvent, ref newCollEvent, ref contacts, in collBallEntity, newTime);

						// write back
						balls[collBallEntity] = collBall;
					}

					marker.End();

				}).Run();
		}

		private static void SaveCollisions(ref CollisionEventData collEvent, ref CollisionEventData newCollEvent,
				ref DynamicBuffer<ContactBufferElement> contacts, in Entity ballEntity, float newTime)
			{
				var validHit = newTime >= 0 && newTime <= collEvent.HitTime;

				if (newCollEvent.IsContact || validHit) {
					newCollEvent.SetCollider(ballEntity);
					newCollEvent.HitTime = newTime;
					if (newCollEvent.IsContact) {
						contacts.Add(new ContactBufferElement(ballEntity, newCollEvent));

					} else {                         // if (validhit)
						collEvent = newCollEvent;
					}
				}
			}
	}
}
