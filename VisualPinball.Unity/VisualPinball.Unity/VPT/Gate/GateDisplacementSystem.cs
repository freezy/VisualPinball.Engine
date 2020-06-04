using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Unity.Physics.SystemGroup;

namespace VisualPinball.Unity.VPT.Gate
{
	[UpdateInGroup(typeof(UpdateDisplacementSystemGroup))]
	public class GateDisplacementSystem : SystemBase
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("GateDisplacementSystem");

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override void OnUpdate()
		{
			var dTime = _simulateCycleSystemGroup.HitTime;
			var marker = PerfMarker;

			Entities.WithName("GateDisplacementJob").ForEach((ref GateMovementData movementData, in GateStaticData data) => {

				marker.Begin();

				if (data.TwoWay) {
					if (math.abs(movementData.Angle) > data.AngleMax) {
						if (movementData.Angle < 0.0) {
							movementData.Angle = -data.AngleMax;
						} else {
							movementData.Angle = data.AngleMax;
						}

						//todo this.events.fireVoidEventParm(Event.LimitEventsEOS, Math.abs(radToDeg(this.angleSpeed)));    // send EOS event
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
						// todo this.events.fireVoidEventParm(Event.LimitEventsEOS, Math.abs(radToDeg(movementData.AngleSpeed)));    // send EOS event
						if (!movementData.ForcedMove) {
							movementData.AngleSpeed = -movementData.AngleSpeed;
							movementData.AngleSpeed *= data.Damping * 0.8f;           // just some extra damping to reduce the angleSpeed a bit faster
						} else if (movementData.AngleSpeed > 0.0) {
							movementData.AngleSpeed = 0.0f;
						}
					}
					if (movementData.Angle < data.AngleMin) {
						movementData.Angle = data.AngleMin;
						// todo this.events.fireVoidEventParm(Event.LimitEventsBOS, Math.abs(radToDeg(movementData.AngleSpeed)));    // send Park event
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
		}
	}
}
