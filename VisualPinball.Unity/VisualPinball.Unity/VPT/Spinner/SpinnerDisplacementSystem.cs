using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Unity.Physics.SystemGroup;

namespace VisualPinball.Unity.VPT.Spinner
{
	[UpdateInGroup(typeof(UpdateDisplacementSystemGroup))]
	public class SpinnerDisplacementSystem : SystemBase
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("SpinnerDisplacementSystem");

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override void OnUpdate()
		{
			var dTime = _simulateCycleSystemGroup.HitTime;
			var marker = PerfMarker;

			Entities
				.WithName("SpinnerDisplacementJob")
				.ForEach((ref SpinnerMovementData movementData, in SpinnerStaticData data) => {

				marker.Begin();

				var angleMin = math.radians(data.AngleMin);
				var angleMax = math.radians(data.AngleMax);

				// blocked spinner, limited motion spinner
				if (data.AngleMin != data.AngleMax) {

					movementData.Angle += movementData.AngleSpeed * dTime;

					if (movementData.Angle > angleMax) {
						movementData.Angle = angleMax;

						// todo event
						//m_pspinner->FireVoidEventParm(DISPID_LimitEvents_EOS, fabsf(RADTOANG(movementData.AngleSpeed)));	// send EOS event

						if (movementData.AngleSpeed > 0.0f) {
							movementData.AngleSpeed *= -0.005f -data.Elasticity;
						}
					}

					if (movementData.Angle < angleMin) {
						movementData.Angle = angleMin;

						// todo event
						// m_pspinner->FireVoidEventParm(DISPID_LimitEvents_BOS, fabsf(RADTOANG(movementData.AngleSpeed)));	// send Park event

						if (movementData.AngleSpeed < 0.0f) {
							movementData.AngleSpeed *= -0.005f - data.Elasticity;
						}
					}

				} else {

					movementData.Angle += movementData.AngleSpeed * dTime;

					// todo event
					// var target = movementData.AngleSpeed > 0.0f
					// 	? movementData.Angle < math.PI ? math.PI : 3.0f * math.PI
					// 	: movementData.Angle < math.PI ? -math.PI : math.PI;
					// if (movementData.AngleSpeed > 0.0f) {
					//
					// 	if (movementData.AngleSpeed > target) {
					// 		m_pspinner->FireGroupEvent(DISPID_SpinnerEvents_Spin);
					// 	}
					//
					// } else {
					// 	if (movementData.AngleSpeed < target) {
					// 		m_pspinner->FireGroupEvent(DISPID_SpinnerEvents_Spin);
					// 	}
					// }

					while (movementData.Angle > 2.0f * math.PI) {
						movementData.Angle -= 2.0f * math.PI;
					}

					while (movementData.Angle < 0.0f) {
						movementData.Angle += 2.0f * math.PI;
					}
				}

				marker.End();

			}).Run();
		}
	}
}
