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
				NormalizeOrientation(ball.Orientation);
				// https://docs.unity.cn/Packages/com.unity.mathematics@1.2/api/Unity.Mathematics.math.orthonormalize.html#Unity_Mathematics_math_orthonormalize_Unity_Mathematics_float3x3_

				// angular momentum = drehimpuls / Schwung, Impulsmomemt
				// angular velocity = Winkelgeschwindigkeit
				ball.AngularVelocity = ball.AngularMomentum / inertia;

				marker.End();

			}).Run();
		}

		private void NormalizeOrientation(float3x3 orientation)
		{
			float lengthX, lengthY, lengthZ = 0f;
			lengthX = math.sqrt(orientation.c0.x * orientation.c0.x + orientation.c1.x * orientation.c1.x + orientation.c2.x * orientation.c2.x);
			lengthY = math.sqrt(orientation.c0.y * orientation.c0.y + orientation.c1.y * orientation.c1.y + orientation.c2.y * orientation.c2.y);
			lengthZ = math.sqrt(orientation.c0.z * orientation.c0.z + orientation.c1.z * orientation.c1.z + orientation.c2.z * orientation.c2.z);
			orientation.c0.x /= lengthX;
			orientation.c1.x /= lengthX;
			orientation.c2.x /= lengthX;
			orientation.c0.y /= lengthY;
			orientation.c1.y /= lengthY;
			orientation.c2.y /= lengthY;
			orientation.c0.z /= lengthZ;
			orientation.c1.z /= lengthZ;
			orientation.c2.z /= lengthZ;
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
