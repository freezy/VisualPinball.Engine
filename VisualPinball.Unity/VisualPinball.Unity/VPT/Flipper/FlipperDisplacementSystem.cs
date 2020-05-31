using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Unity.Physics.SystemGroup;

namespace VisualPinball.Unity.VPT.Flipper
{
	public struct FlipperRotatedEvent
	{
		public bool Direction;
		public float AngleSpeed;
		public int EntityIndex;
	}

	[AlwaysSynchronizeSystem]
	[UpdateInGroup(typeof(UpdateDisplacementSystemGroup))]
	public class FlipperDisplacementSystem : SystemBase
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("FlipperDisplacementSystem");

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override void OnUpdate()
		{
			var dTime = _simulateCycleSystemGroup.HitTime;
			var marker = PerfMarker;

			Entities.WithName("FlipperDisplacementJob").ForEach((ref FlipperMovementData state, in FlipperStaticData data) => {

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
						//_events.FireVoidEventParam(Event.LimitEventsEOS, angleSpeed); // send EOS event

					} else if (state.EnableRotateEvent < 0) {
						//_events.FireVoidEventParam(Event.LimitEventsBOS, angleSpeed); // send Beginning of Stroke/Park event
					}

					state.EnableRotateEvent = 0;
				}

				marker.End();
			}).Run();
		}
	}
}
