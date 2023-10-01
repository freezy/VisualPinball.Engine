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
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateDisplacementSystemGroup))]
	internal partial class GateDisplacementSystem : SystemBase
	{
		private Player _player;
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private NativeQueue<EventData> _eventQueue;

		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("GateDisplacementSystem");

		protected override void OnCreate()
		{
			_player = Object.FindObjectOfType<Player>();
			_simulateCycleSystemGroup = World.GetOrCreateSystemManaged<SimulateCycleSystemGroup>();
			_eventQueue = new NativeQueue<EventData>(Allocator.Persistent);
		}

		protected override void OnDestroy()
		{
			_eventQueue.Dispose();
		}

		protected override void OnUpdate()
		{
			var dTime = _simulateCycleSystemGroup.HitTime;
			var marker = PerfMarker;
			var events = _eventQueue.AsParallelWriter();

			Entities
				.WithName("GateDisplacementJob")
				.ForEach((Entity entity, ref GateMovementData movementData, in GateStaticData data) => {

				marker.Begin();

				if (data.TwoWay) {
					if (math.abs(movementData.Angle) > data.AngleMax) {
						if (movementData.Angle < 0.0) {
							movementData.Angle = -data.AngleMax;
						} else {
							movementData.Angle = data.AngleMax;
						}

						// send EOS event
						events.Enqueue(new EventData(EventId.LimitEventsEos, entity, movementData.AngleSpeed));

						if (!movementData.ForcedMove) {
							movementData.AngleSpeed = -movementData.AngleSpeed;
							movementData.AngleSpeed *= data.Damping * 0.8f;           // just some extra damping to reduce the angleSpeed a bit faster
						} else if (movementData.AngleSpeed > 0.0) {
							movementData.AngleSpeed = 0.0f;
						}
					}
					if (math.abs(movementData.Angle) < data.AngleMin) {
						if (movementData.Angle < 0.0) {
							movementData.Angle = -data.AngleMin;
						} else {
							movementData.Angle = data.AngleMin;
						}
						if (!movementData.ForcedMove) {
							movementData.AngleSpeed = -movementData.AngleSpeed;
							movementData.AngleSpeed *= data.Damping * 0.8f;           // just some extra damping to reduce the angleSpeed a bit faster
						} else if (movementData.AngleSpeed < 0.0) {
							movementData.AngleSpeed = 0.0f;
						}
					}
				} else {
					var direction = movementData.HitDirection ? -1f : 1f;
					if (direction * movementData.Angle > data.AngleMax) {
						movementData.Angle = direction * data.AngleMax;

						// send EOS event
						events.Enqueue(new EventData(EventId.LimitEventsEos, entity, movementData.AngleSpeed));

						if (!movementData.ForcedMove) {
							movementData.AngleSpeed = -movementData.AngleSpeed;
							movementData.AngleSpeed *= data.Damping * 0.8f;           // just some extra damping to reduce the angleSpeed a bit faster
						} else if (movementData.AngleSpeed > 0.0) {
							movementData.AngleSpeed = 0.0f;
						}
					}
					if (direction * movementData.Angle < data.AngleMin) {
						movementData.Angle = direction * data.AngleMin;

						// send Park event
						events.Enqueue(new EventData(EventId.LimitEventsBos, entity, movementData.AngleSpeed));

						if (!movementData.ForcedMove) {
							movementData.AngleSpeed = -movementData.AngleSpeed;
							movementData.AngleSpeed *= data.Damping * 0.8f;           // just some extra damping to reduce the angleSpeed a bit faster
						} else if (movementData.AngleSpeed < 0.0) {
							movementData.AngleSpeed = 0.0f;
						}
					}
				}
				movementData.Angle += movementData.AngleSpeed * dTime;
				
				if (movementData.IsLifting) {
					if (math.abs(movementData.Angle - movementData.LiftAngle) > 0.000001f) {
						var direction = movementData.Angle < movementData.LiftAngle ? 1f : -1f;
						movementData.Angle += direction * (movementData.LiftSpeed * dTime);

					} else {
						movementData.IsLifting = false;
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
