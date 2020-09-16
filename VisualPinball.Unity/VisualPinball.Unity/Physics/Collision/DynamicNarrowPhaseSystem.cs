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
		private EntityQuery _collDataEntityQuery;

		protected override void OnCreate()
		{
			_collDataEntityQuery = EntityManager.CreateEntityQuery(typeof(ColliderData));
		}

		protected override void OnUpdate()
		{
			var balls = GetComponentDataFromEntity<BallData>();
			var overlappingEntitiesBuffer = GetBufferFromEntity<OverlappingDynamicBufferElement>(true);
			var contactsLookup = GetBufferFromEntity<ContactBufferElement>();
			var collEntity = _collDataEntityQuery.GetSingletonEntity();

			var marker = PerfMarker;

			Entities
				.WithName("DynamicNarrowPhaseJob")
				.WithNativeDisableParallelForRestriction(balls)
				.WithReadOnly(overlappingEntitiesBuffer)
				.ForEach((Entity ballEntity, ref BallData ball, ref CollisionEventData collEvent) => {

					// don't play with frozen balls
					if (ball.IsFrozen) {
						return;
					}

					marker.Begin();

					var contacts = contactsLookup[collEntity];

					var overlappingEntities = overlappingEntitiesBuffer[ballEntity];
					for (var k = 0; k < overlappingEntities.Length; k++) {
						var collBallEntity = overlappingEntities[k].Value;
						var collBall = balls[collBallEntity];

						var newCollEvent = new CollisionEventData();
						//var newTime = BallCollider.HitTest(ref newCollEvent, ref collBall, in ball, collEvent.HitTime);
						var newTime = BallCollider.HitTest(ref newCollEvent, ref ball, in collBall, collEvent.HitTime);
						var validHit = newTime >= 0 && !Math.Sign(newTime) && newTime <= collEvent.HitTime;

						if (newCollEvent.IsContact || validHit) {
							newCollEvent.SetCollider(ballEntity);
							newCollEvent.HitTime = newTime;
							if (newCollEvent.IsContact) {
								contacts.Add(new ContactBufferElement(ballEntity, newCollEvent));

							} else { // if (validhit)
								collEvent = newCollEvent;
							}
						}

						// write back
						balls[collBallEntity] = collBall;
					}

					marker.End();

				}).Run();
		}
	}
}
