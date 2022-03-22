// Visual Pinball Engineball.Orientation
// Copyright (C) 2022 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;

namespace VisualPinball.Unity
{
	[UpdateInGroup(typeof(UpdateDisplacementSystemGroup))]
	internal class BallDisplacementSystem : SystemBase
	{
		private SimulateCycleSystemGroup _simulateCycleSystemGroup;

		private static readonly ProfilerMarker PerfMarker = new ProfilerMarker("BallDisplacementSystem");

		protected override void OnCreate()
		{
			_simulateCycleSystemGroup = World.GetOrCreateSystem<SimulateCycleSystemGroup>();
		}

		protected override void OnUpdate()
		{
			var dTime = _simulateCycleSystemGroup.HitTime;
			var marker = PerfMarker;

			Entities.WithName("BallDisplacementJob").ForEach((ref BallData ball) => {

				if (ball.IsFrozen) {
					return;
				}

				marker.Begin();

				ball.Position += ball.Velocity * dTime;

				//Logger.Debug($"Ball {ball.Id} Position = {ball.Position}");

				var inertia = ball.Inertia;
				var mat3 = CreateSkewSymmetric(ball.AngularMomentum / inertia);
				var addedOrientation = math.mul(ball.Orientation, mat3);
				addedOrientation *= dTime;

				ball.Orientation += addedOrientation;
				math.orthonormalize(ball.Orientation);

				// after Orthonormalization, the orientation vectors also have to be normalized - this is not done by othonomalize, since the skew matrix creates quite lengthy vectors.
				// in fact, they dont have to be normalized, but just shortened, so we can abs-add the x, y and z together and just divide by the sum.
				// This saves three sqrts in the game loop per ball. 
				float lengthX, lengthY, lengthZ;
				/* Correct normalization would be: 
				 * lengthX = math.sqrt(ball.Orientation.c0.x * ball.Orientation.c0.x + ball.Orientation.c0.y * ball.Orientation.c0.y + ball.Orientation.c0.z * ball.Orientation.c0.z);
				 * lengthY = math.sqrt(ball.Orientation.c1.x * ball.Orientation.c1.x + ball.Orientation.c1.y * ball.Orientation.c1.y + ball.Orientation.c1.z * ball.Orientation.c1.z);
				 * lengthZ = math.sqrt(ball.Orientation.c2.x * ball.Orientation.c2.x + ball.Orientation.c2.y * ball.Orientation.c2.y + ball.Orientation.c2.z * ball.Orientation.c2.z);
				 */
				lengthX = math.abs(ball.Orientation.c0.x) + math.abs(ball.Orientation.c0.y) + math.abs(ball.Orientation.c0.z);
				lengthY = math.abs(ball.Orientation.c1.x) + math.abs(ball.Orientation.c1.y) + math.abs(ball.Orientation.c1.z);
				lengthZ = math.abs(ball.Orientation.c2.x) + math.abs(ball.Orientation.c2.y) + math.abs(ball.Orientation.c2.z);
				if (lengthX != 0f)
				{
					ball.Orientation.c0.x /= lengthX;
					ball.Orientation.c0.y /= lengthX;
					ball.Orientation.c0.z /= lengthX;
				}
				if (lengthY != 0f)
				{
					ball.Orientation.c1.x /= lengthY;
					ball.Orientation.c1.y /= lengthY;
					ball.Orientation.c1.z /= lengthY;
				}
				if (lengthZ != 0f)
				{
					ball.Orientation.c2.x /= lengthZ;
					ball.Orientation.c2.y /= lengthZ;
					ball.Orientation.c2.z /= lengthZ;
				}

				ball.AngularVelocity = ball.AngularMomentum / inertia;

				marker.End();

			}).Run();
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
