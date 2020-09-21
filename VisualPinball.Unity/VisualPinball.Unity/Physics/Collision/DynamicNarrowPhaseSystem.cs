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
using Unity.Jobs;
using Unity.Profiling;

namespace VisualPinball.Unity
{
	[DisableAutoCreation]
	internal class DynamicNarrowPhaseSystem : SystemBase
	{
		public JobHandle Dep => Dependency;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("DynamicNarrowPhaseSystem");
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override void OnUpdate()
		{
			var ballsLookup = GetComponentDataFromEntity<BallData>();
			var contacts = _simulateCycleSystemGroup.Contacts;

			var marker = PerfMarker;

			Entities
				.WithName("DynamicNarrowPhaseJob")
				.WithReadOnly(contacts)
				.WithNativeDisableParallelForRestriction(ballsLookup)
				.ForEach((Entity ballEntity, ref BallData ball, ref CollisionEventData collEvent,
					in DynamicBuffer<OverlappingDynamicBufferElement> overlappingEntities) =>
				{
					// don't play with frozen balls
					if (ball.IsFrozen) {
						return;
					}

					marker.Begin();

					//var contacts = contactsLookup[collDataEntity];
					for (var k = 0; k < overlappingEntities.Length; k++) {
						var collBallEntity = overlappingEntities[k].Value;
						var collBall = ballsLookup[collBallEntity];

						var newCollEvent = new CollisionEventData();
						var newTime = BallCollider.HitTest(ref newCollEvent, ref ball, in collBall, collEvent.HitTime);
						var validHit = newTime >= 0 && !Math.Sign(newTime) && newTime <= collEvent.HitTime;

						if (newCollEvent.IsContact || validHit) {
							newCollEvent.SetCollider(collBallEntity);
							newCollEvent.HitTime = newTime;
							if (newCollEvent.IsContact) {
								contacts.Add(new ContactBufferElement(ballEntity, newCollEvent));

							} else { // if (validhit)
								collEvent = newCollEvent;
							}
						}

						// write back
						ballsLookup[collBallEntity] = collBall;
					}

					marker.End();

				}
			).ScheduleParallel();
		}
	}
}
