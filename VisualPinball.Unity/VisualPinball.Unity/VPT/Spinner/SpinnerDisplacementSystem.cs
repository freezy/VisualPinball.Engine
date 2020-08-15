// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateDisplacementSystemGroup))]
	public class SpinnerDisplacementSystem : SystemBase
	{
		private Player _player;
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private NativeQueue<EventData> _eventQueue;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("SpinnerDisplacementSystem");

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

			var events = _eventQueue.AsParallelWriter();

			var dTime = _simulateCycleSystemGroup.HitTime;
			var marker = PerfMarker;

			Entities
				.WithName("SpinnerDisplacementJob")
				.ForEach((Entity entity, ref SpinnerMovementData movementData, in SpinnerStaticData data) => {

				marker.Begin();

				// those are already converted to radian during authoring.
				var angleMin = data.AngleMin;
				var angleMax = data.AngleMax;

				// blocked spinner, limited motion spinner
				if (data.AngleMin != data.AngleMax) {

					movementData.Angle += movementData.AngleSpeed * dTime;

					if (movementData.Angle > angleMax) {
						movementData.Angle = angleMax;

						// send EOS event
						events.Enqueue(new EventData(EventId.LimitEventsEos, entity, math.abs(math.degrees(movementData.AngleSpeed))));

						if (movementData.AngleSpeed > 0.0f) {
							movementData.AngleSpeed *= -0.005f - data.Elasticity;
						}
					}

					if (movementData.Angle < angleMin) {
						movementData.Angle = angleMin;

						// send Park event
						events.Enqueue(new EventData(EventId.LimitEventsBos, entity, math.abs(math.degrees(movementData.AngleSpeed))));

						if (movementData.AngleSpeed < 0.0f) {
							movementData.AngleSpeed *= -0.005f - data.Elasticity;
						}
					}

				} else {

					var target = movementData.AngleSpeed > 0.0f
						? movementData.Angle < math.PI ? math.PI : 3.0f * math.PI
						: movementData.Angle < math.PI ? -math.PI : math.PI;

					movementData.Angle += movementData.AngleSpeed * dTime;

					if (movementData.AngleSpeed > 0.0f) {

						if (movementData.Angle > target) {
							events.Enqueue(new EventData(EventId.SpinnerEventsSpin, entity, true));
						}

					} else {
						if (movementData.Angle < target) {
							events.Enqueue(new EventData(EventId.SpinnerEventsSpin, entity, true));
						}
					}

					while (movementData.Angle > 2.0f * math.PI) {
						movementData.Angle -= 2.0f * math.PI;
					}

					while (movementData.Angle < 0.0f) {
						movementData.Angle += 2.0f * math.PI;
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
