// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using Unity.Entities;
using Unity.Profiling;

namespace VisualPinball.Unity
{
	[DisableAutoCreation]
	internal class DynamicNarrowPhaseSystem : SystemBase
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

					// don't play with frozen balls
					if (ball.IsFrozen) {
						return;
					}

					marker.Begin();

					var contacts = contactsBuffer[entity];
					var overlappingEntities = overlappingEntitiesBuffer[entity];
					for (var k = 0; k < overlappingEntities.Length; k++) {
						var collBallEntity = overlappingEntities[k].Value;
						var collBall = balls[collBallEntity];

						var newCollEvent = new CollisionEventData();
						var newTime = BallCollider.HitTest(ref newCollEvent, ref ball, in collBall, collEvent.HitTime);

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
				var validHit = newTime >= 0 && !Math.Sign(newTime) && newTime <= collEvent.HitTime;

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
