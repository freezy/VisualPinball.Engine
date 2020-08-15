using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Engine.Common;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateVelocitiesSystemGroup))]
	public class SpinnerVelocitySystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("SpinnerVelocitySystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			Entities
				.WithName("SpinnerVelocityJob")
				.ForEach((ref SpinnerMovementData movementData, in SpinnerStaticData data) => {

				marker.Begin();

				// Center of gravity towards bottom of object, makes it stop vertical
				movementData.AngleSpeed -= math.sin(movementData.Angle) * (float)(0.0025 * PhysicsConstants.PhysFactor);
				movementData.AngleSpeed *= data.Damping;

				marker.End();

			}).Run();
		}
	}
}
