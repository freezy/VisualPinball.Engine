using Unity.Collections;
using Unity.Entities;
using Unity.Profiling;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateAnimationsSystemGroup))]
	public class HitTargetAnimationSystem : SystemBase
	{
		private VisualPinballSimulationSystemGroup _visualPinballSimulationSystemGroup;
		private NativeQueue<EventData> _eventQueue;

		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("HitTargetAnimationSystem");

		protected override void OnCreate()
		{
			_visualPinballSimulationSystemGroup = World.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();
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
				.ForEach((Entity entity, ref HitTargetAnimationData data, ref HitTargetMovementData movementData,
					in HitTargetStaticData staticData) =>
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

				if (staticData.IsDropTarget) {
					if (data.MoveAnimation) {
						var step = staticData.DropSpeed * staticData.TableScaleZ;
						var limit = HitTargetMovementData.DropTargetLimit * staticData.TableScaleZ;

						if (data.MoveDown) {
							step = -step;

						} else if (data.TimeMsec - data.TimeStamp < (uint) staticData.RaiseDelay) {
							step = 0.0f;
						}

						movementData.ZOffset += step * diffTimeMsec;
						if (data.MoveDown) {
							if (movementData.ZOffset <= -limit) {
								movementData.ZOffset = -limit;
								data.MoveDown = false;
								data.IsDropped = true;
								data.MoveAnimation = false;
								data.TimeStamp = 0;
								if (staticData.UseHitEvent) {
									events.Enqueue(new EventData(EventId.TargetEventsDropped, entity));
								}
							}

						} else {
							if (movementData.ZOffset >= 0.0f) {
								movementData.ZOffset = 0.0f;
								data.MoveAnimation = false;
								data.IsDropped = false;
								if (staticData.UseHitEvent) {
									events.Enqueue(new EventData(EventId.TargetEventsRaised, entity));
								}
							}
						}
					}

				} else {
					if (data.MoveAnimation) {
						var step = staticData.DropSpeed * staticData.TableScaleZ;
						var limit = 13.0f * staticData.TableScaleZ;
						if (!data.MoveDown) {
							step = -step;
						}

						movementData.XRotation += step * diffTimeMsec;
						if (data.MoveDown) {
							if (movementData.XRotation >= limit) {
								movementData.XRotation = limit;
								data.MoveDown = false;
							}

						} else {
							if (movementData.XRotation <= 0.0f) {
								movementData.XRotation = 0.0f;
								data.MoveAnimation = false;
							}
						}
					}
				}

				marker.End();

			}).Run();
		}
	}
}
