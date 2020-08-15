using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateDisplacementSystemGroup))]
	public class GateDisplacementSystem : SystemBase
	{
		private Player _player;
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private NativeQueue<EventData> _eventQueue;

		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("GateDisplacementSystem");

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
					if (movementData.Angle > data.AngleMax) {
						movementData.Angle = data.AngleMax;

						// send EOS event
						events.Enqueue(new EventData(EventId.LimitEventsEos, entity, movementData.AngleSpeed));

						if (!movementData.ForcedMove) {
							movementData.AngleSpeed = -movementData.AngleSpeed;
							movementData.AngleSpeed *= data.Damping * 0.8f;           // just some extra damping to reduce the angleSpeed a bit faster
						} else if (movementData.AngleSpeed > 0.0) {
							movementData.AngleSpeed = 0.0f;
						}
					}
					if (movementData.Angle < data.AngleMin) {
						movementData.Angle = data.AngleMin;

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

				marker.End();

			}).Run();

			// dequeue events
			while (_eventQueue.TryDequeue(out var eventData)) {
				_player.OnEvent(in eventData);
			}
		}
	}
}
