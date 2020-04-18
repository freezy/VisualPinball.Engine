using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using VisualPinball.Unity.Physics.SystemGroup;

namespace VisualPinball.Unity.VPT.Ball
{
	[UpdateInGroup(typeof(UpdateDisplacementSystemGroup))]
	public class BallDisplacementSystem : JobComponentSystem
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var dTime = (float) _simulateCycleSystemGroup.DTime;

			return Entities.ForEach((ref BallData ball) => {

				if (ball.IsFrozen) {
					return;
				}

				ball.Position += ball.Velocity * dTime;

				// todo rotation
				var mat3 = CreateSkewSymmetric(ball.AngularVelocity);
				// var addedOrientation = new float3x3();
				// addedOrientation.MultiplyMatrix(mat3, ball.Orientation);
				// addedOrientation.MultiplyScalar(dTime);
				//
				// _state.Orientation.AddMatrix(addedOrientation, _state.Orientation);
				// _state.Orientation.OrthoNormalize();

				ball.AngularVelocity = ball.AngularMomentum / ball.Inertia;

			}).Schedule(inputDeps);
		}

		private static float3x3 CreateSkewSymmetric(in float3 pv3D)
		{
			return new float3x3(
				0, -pv3D.z, pv3D.y,
				pv3D.z, 0, -pv3D.x,
				-pv3D.y, pv3D.x, 0
			);
		}
	}
}
