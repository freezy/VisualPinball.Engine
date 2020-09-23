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

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	[AlwaysSynchronizeSystem]
	[UpdateInGroup(typeof(UpdateDisplacementSystemGroup))]
	internal class FlipperDisplacementSystem : SystemBase
	{
		private Player _player;
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private NativeQueue<EventData> _eventQueue;

		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("FlipperDisplacementSystem");

		protected override void OnCreate()
		{
			_player = Object.FindObjectOfType<Player>();
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
			_eventQueue = new NativeQueue<EventData>(Allocator.Persistent);
		}

		protected override void OnDestroy()
		{
			_eventQueue.Dispose();
		}

		protected override void OnUpdate()
		{
			var dTime = _simulateCycleSystemGroup.HitTime;
			var events = _eventQueue.AsParallelWriter();
			var marker = PerfMarker;

			Entities.WithName("FlipperDisplacementJob").ForEach((Entity entity, ref FlipperMovementData state, in FlipperStaticData data) => {

				marker.Begin();

				state.Angle += state.AngleSpeed * dTime; // move flipper angle

				var angleMin = math.min(data.AngleStart, data.AngleEnd);
				var angleMax = math.max(data.AngleStart, data.AngleEnd);

				if (state.Angle > angleMax) {
					state.Angle = angleMax;
				}

				if (state.Angle < angleMin) {
					state.Angle = angleMin;
				}

				if (math.abs(state.AngleSpeed) < 0.0005f) {
					// avoids "jumping balls" when two or more balls held on flipper (and more other balls are in play) //!! make dependent on physics update rate
					marker.End();
					return;
				}

				var handleEvent = false;

				if (state.Angle >= angleMax) {
					// hit stop?
					if (state.AngleSpeed > 0) {
						handleEvent = true;
					}

				} else if (state.Angle <= angleMin) {
					if (state.AngleSpeed < 0) {
						handleEvent = true;
					}
				}

				if (handleEvent) {
					var angleSpeed = math.abs(math.degrees(state.AngleSpeed));
					state.AngularMomentum *= -0.3f; // make configurable?
					state.AngleSpeed = state.AngularMomentum / data.Inertia;

					if (state.EnableRotateEvent > 0) {

						// send EOS event
						events.Enqueue(new EventData(EventId.LimitEventsEos, entity, angleSpeed));

					} else if (state.EnableRotateEvent < 0) {

						// send BOS event
						events.Enqueue(new EventData(EventId.LimitEventsBos, entity, angleSpeed));
					}

					state.EnableRotateEvent = 0;
				}

				marker.End();

			}).ScheduleParallel();

			// dequeue events
			while (_eventQueue.TryDequeue(out var eventData)) {
				_player.OnEvent(in eventData);
			}
		}
	}
}
