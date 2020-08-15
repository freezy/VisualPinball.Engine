// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateVelocitiesSystemGroup))]
	public class GateVelocitySystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("FlipperVelocitySystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			Entities
				.WithName("GateVelocityJob")
				.ForEach((ref GateMovementData movementData, in GateStaticData data) => {

				marker.Begin();

				if (!movementData.IsOpen) {
					if (math.abs(movementData.Angle) < data.AngleMin + 0.01f && math.abs(movementData.AngleSpeed) < 0.01f) {
						// stop a bit earlier to prevent a nearly endless animation (especially for slow balls)
						movementData.Angle = data.AngleMin;
						movementData.AngleSpeed = 0.0f;
					}
					if (math.abs(movementData.AngleSpeed) != 0.0f && movementData.Angle != data.AngleMin) {
						movementData.AngleSpeed -= math.sin(movementData.Angle) * data.GravityFactor * (PhysicsConstants.PhysFactor / 100.0f); // Center of gravity towards bottom of object, makes it stop vertical
						movementData.AngleSpeed *= data.Damping;
					}
				}

				marker.End();

			}).Run();
		}
	}
}
