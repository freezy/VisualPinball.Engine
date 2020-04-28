using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using VisualPinball.Unity.Physics.SystemGroup;

namespace VisualPinball.Unity.VPT.Ball
{
	[UpdateInGroup(typeof(UpdateDisplacementSystemGroup))]
	public class BallDisplacementSystem : SystemBase
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override void OnUpdate()
		{
			var dTime = _simulateCycleSystemGroup.HitTime;

			Entities.WithName("BallDisplacementJob").ForEach((ref BallData ball) => {

				if (ball.IsFrozen) {
					return;
				}

				ball.Position += ball.Velocity * dTime;

				var inertia = ball.Inertia;
				var mat3 = CreateSkewSymmetric(ball.AngularMomentum / inertia);
				var addedOrientation = math.mul(ball.Orientation, mat3);
				addedOrientation *= dTime;

				ball.Orientation += addedOrientation;
				math.orthonormalize(ball.Orientation);

				ball.AngularVelocity = ball.AngularMomentum / inertia;

			}).ScheduleParallel();
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
