// ReSharper disable CompareOfFloatsByEqualityOperator

using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Physics.SystemGroup;

namespace VisualPinball.Unity.VPT.Spinner
{
	[UpdateInGroup(typeof(UpdateVelocitiesSystemGroup))]
	public class SpinnerVelocitySystem : SystemBase
	{
		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("FlipperVelocitySystem");

		protected override void OnUpdate()
		{
			var marker = PerfMarker;
			Entities
				.WithName("SpinnerVelocityJob")
				.ForEach((ref SpinnerMovementData movementData, in SpinnerStaticData data) => {

				marker.Begin();

				movementData.AngleSpeed -= math.sin(movementData.AngleSpeed) * (float)(0.0025 * PhysicsConstants.PhysFactor); // Center of gravity towards bottom of object, makes it stop vertical
				movementData.AngleSpeed *= data.Damping;

				marker.End();

			}).Run();
		}
	}
}
