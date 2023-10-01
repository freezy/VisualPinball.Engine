// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using Unity.Profiling;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateAnimationsSystemGroup))]
	internal class DropTargetAnimationSystem : SystemBase
	{
		private Player _player;
		private VisualPinballSimulationSystemGroup _visualPinballSimulationSystemGroup;
		private NativeQueue<EventData> _eventQueue;

		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("HitTargetAnimationSystem");

		protected override void OnCreate()
		{
			_player = Object.FindObjectOfType<Player>();
			_visualPinballSimulationSystemGroup = World.GetOrCreateSystemManaged<VisualPinballSimulationSystemGroup>();
			_eventQueue = new NativeQueue<EventData>(Allocator.Persistent);
		}

		protected override void OnDestroy()
		{
			_eventQueue.Dispose();
		}

		protected override void OnUpdate()
		{
			var timeMsec = _visualPinballSimulationSystemGroup.TimeMsec;
			var events = _eventQueue.AsParallelWriter();
			var marker = PerfMarker;

			Entities
				.WithName("HitTargetAnimationJob")
				.ForEach((Entity entity, ref DropTargetAnimationData data, in DropTargetStaticData staticData) =>
			{
				marker.Begin();

				var oldTimeMsec = data.TimeMsec < timeMsec ? data.TimeMsec : timeMsec;
				data.TimeMsec = timeMsec;
				var diffTimeMsec = (float)(timeMsec - oldTimeMsec);

				if (data.HitEvent) {
					if (!data.IsDropped) {
						data.MoveDown = true;
					}

					data.MoveAnimation = true;
					data.HitEvent = false;
				}

				if (data.MoveAnimation) {
					var step = staticData.Speed;

					if (data.MoveDown) {
						step = -step;

					} else if (data.TimeMsec - data.TimeStamp < (uint) staticData.RaiseDelay) {
						step = 0.0f;
					}

					data.ZOffset += step * diffTimeMsec;
					if (data.MoveDown) {
						if (data.ZOffset <= -DropTargetAnimationData.DropTargetLimit) {
							data.ZOffset = -DropTargetAnimationData.DropTargetLimit;
							data.MoveDown = false;
							data.IsDropped = true;
							data.MoveAnimation = false;
							data.TimeStamp = 0;
							if (staticData.UseHitEvent) {
								events.Enqueue(new EventData(EventId.TargetEventsDropped, entity));
							}
						}

					} else {
						if (data.ZOffset >= 0.0f) {
							data.ZOffset = 0.0f;
							data.MoveAnimation = false;
							data.IsDropped = false;
							if (staticData.UseHitEvent) {
								events.Enqueue(new EventData(EventId.TargetEventsRaised, entity));
							}
						}
					}
				}

				marker.End();

			}).Run();

			// dequeue events
			while (_eventQueue.TryDequeue(out var eventData)) {
				_player.OnEvent(in eventData);
			}
		}
	}
}
