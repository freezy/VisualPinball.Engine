using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
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
	public class FlipperDisplacementSystem : JobComponentSystem
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var dTime = _simulateCycleSystemGroup.HitTime;

			Entities.WithoutBurst().WithName("FlipperDisplacementJob").ForEach((ref FlipperMovementData state, in FlipperMaterialData data) => {

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
			}).Run();

			return default;
		}
	}
}
