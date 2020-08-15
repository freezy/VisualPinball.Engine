using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateDisplacementSystemGroup))]
	public class PlungerDisplacementSystem : SystemBase
	{
		private Player _player;
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private NativeQueue<EventData> _eventQueue;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("PlungerDisplacementSystem");

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

			Entities.WithName("PlungerDisplacementJob").ForEach((Entity entity, ref PlungerMovementData movementData,
				ref PlungerColliderData colliderData, in PlungerStaticData staticData) =>
			{
				marker.Begin();

				// figure the travel distance
				var dx = dTime * movementData.Speed;

				// figure the position change
				movementData.Position += dx;

				// apply the travel limit
				if (movementData.Position < movementData.TravelLimit) {
					movementData.Position = movementData.TravelLimit;
				}

				// if we're in firing mode and we've crossed the bounce position, reverse course
				var relPos = (movementData.Position - staticData.FrameEnd) / staticData.FrameLen;
				var bouncePos = staticData.RestPosition + movementData.FireBounce;
				if (movementData.FireTimer != 0 && dTime != 0.0f &&
				    (movementData.FireSpeed < 0.0f ? relPos <= bouncePos : relPos >= bouncePos))
				{
					// stop at the bounce position
					movementData.Position = staticData.FrameEnd + bouncePos * staticData.FrameLen;

					// reverse course at reduced speed
					movementData.FireSpeed = -movementData.FireSpeed * 0.4f;

					// figure the new bounce as a fraction of the previous bounce
					movementData.FireBounce *= -0.4f;
				}

				// apply the travel limit (again)
				if (movementData.Position < movementData.TravelLimit) {
					movementData.Position = movementData.TravelLimit;
				}

				// limit motion to the valid range
				if (dTime != 0.0f) {

					if (movementData.Position < staticData.FrameEnd) {
						movementData.Speed = 0.0f;
						movementData.Position = staticData.FrameEnd;

					} else if (movementData.Position > staticData.FrameStart) {
						movementData.Speed = 0.0f;
						movementData.Position = staticData.FrameStart;
					}

					// apply the travel limit (yet again)
					if (movementData.Position < movementData.TravelLimit) {
						movementData.Position = movementData.TravelLimit;
					}
				}

				// the travel limit applies to one displacement update only - reset it
				movementData.TravelLimit = staticData.FrameEnd;

				// fire an Start/End of Stroke events, as appropriate
				var strokeEventLimit = staticData.FrameLen / 50.0f;
				var strokeEventHysteresis = strokeEventLimit * 2.0f;
				if (movementData.StrokeEventsArmed && movementData.Position + dx > staticData.FrameStart - strokeEventLimit) {
					events.Enqueue(new EventData(EventId.LimitEventsBos, entity, math.abs(movementData.Speed)));
					movementData.StrokeEventsArmed = false;

				} else if (movementData.StrokeEventsArmed && movementData.Position + dx < staticData.FrameEnd + strokeEventLimit) {
					events.Enqueue(new EventData(EventId.LimitEventsEos, entity, math.abs(movementData.Speed)));
					movementData.StrokeEventsArmed = false;

				} else if (movementData.Position > staticData.FrameEnd + strokeEventHysteresis && movementData.Position < staticData.FrameStart - strokeEventHysteresis) {
					// away from the limits - arm the stroke events
					movementData.StrokeEventsArmed = true;
				}

				// update the display
				UpdateCollider(movementData.Position, ref colliderData);

				marker.End();
			}).Run();

			// dequeue events
			while (_eventQueue.TryDequeue(out var eventData)) {
				_player.OnEvent(in eventData);
			}
		}

		private static void UpdateCollider(float len, ref PlungerColliderData colliderData)
		{
			colliderData.LineSegSide0.V1y = len;
			colliderData.LineSegSide1.V2y = len;

			colliderData.LineSegEnd.V2y = len;
			colliderData.LineSegEnd.V1y = len; // + 0.0001f;

			colliderData.JointEnd0.XyY = len;
			colliderData.JointEnd1.XyY = len; // + 0.0001f;

			colliderData.LineSegSide0.CalcNormal();
			colliderData.LineSegSide1.CalcNormal();
			colliderData.LineSegEnd.CalcNormal();
		}
	}
}
