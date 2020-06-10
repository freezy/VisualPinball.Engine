using Unity.Entities;
using Unity.Profiling;
using VisualPinball.Unity.Physics.SystemGroup;

namespace VisualPinball.Unity.VPT.Plunger
{
	[UpdateInGroup(typeof(UpdateDisplacementSystemGroup))]
	public class PlungerDisplacementSystem : SystemBase
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("PlungerDisplacementSystem");

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override void OnUpdate()
		{
			var dTime = _simulateCycleSystemGroup.HitTime;
			var marker = PerfMarker;

			Entities.WithName("FlipperDisplacementJob").ForEach(
				(ref PlungerMovementData movementData, ref PlungerColliderData colliderData, in PlungerStaticData staticData) =>
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

					// todo fire an Start/End of Stroke events, as appropriate
					// var strokeEventLimit = staticData.FrameLen / 50.0f;
					// var strokeEventHysteresis = strokeEventLimit * 2.0f;
					// if (m_strokeEventsArmed && movementData.Position + dx > staticData.FrameStart - strokeEventLimit) {
					// 	m_plunger->FireVoidEventParm(DISPID_LimitEvents_BOS, fabsf(movementData.Speed));
					// 	m_strokeEventsArmed = false;
					//
					// } else if (m_strokeEventsArmed && movementData.Position + dx < staticData.FrameEnd + strokeEventLimit) {
					// 	m_plunger->FireVoidEventParm(DISPID_LimitEvents_EOS, fabsf(movementData.Speed));
					// 	m_strokeEventsArmed = false;
					// } else if (movementData.Position > staticData.FrameEnd + strokeEventHysteresis
					//          && movementData.Position < staticData.FrameStart - strokeEventHysteresis)
					// {
					// 	// away from the limits - arm the stroke events
					// 	m_strokeEventsArmed = true;
					// }

					// update the display
					UpdateCollider(movementData.Position, ref colliderData);

					marker.End();
				}).Run();
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
