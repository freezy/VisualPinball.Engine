// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Unity;

namespace VisualPinballUnity
{
	internal static class BallDisplacementPhysics
	{
		internal static void UpdateDisplacements(ref BallData ball, float dTime)
		{
			if (ball.IsFrozen) {
				return;
			}

			ball.Position += ball.Velocity * dTime;

			var inertia = ball.Inertia;
			var mat3 = CreateSkewSymmetric(ball.AngularMomentum / inertia);
			var addedOrientation = math.mul(ball.BallOrientation, mat3);
			addedOrientation *= dTime;

			ball.BallOrientation += addedOrientation;

			// do the same for Unity's ball Orientation (where z (and z only rotation) has to be flipped),
			// which (maybe??) can't be done after skew matrix operations (or we don't know how))
			// If we flip an exis in the matrix, we always flip two rotations.
			var AngMomFlippedZ = new float3(ball.AngularMomentum.x, ball.AngularMomentum.y, -ball.AngularMomentum.z);
			mat3 = CreateSkewSymmetric(AngMomFlippedZ / inertia);
			addedOrientation = math.mul(ball.BallOrientationForUnity, mat3);
			addedOrientation *= dTime;

			ball.BallOrientationForUnity += addedOrientation;

			VPOrthonormalize(ref ball.BallOrientation);
			VPOrthonormalize(ref ball.BallOrientationForUnity);
		}

		private static void VPOrthonormalize(ref float3x3 orientation) 
		{
			Vector3 vX = new Vector3(orientation.c0.x, orientation.c1.x, orientation.c2.x);
			Vector3 vY = new Vector3(orientation.c0.y, orientation.c1.y, orientation.c2.y);
			Vector3 vZ = Vector3.Cross(vX, vY);
			vX = Vector3.Normalize(vX);
			vZ = Vector3.Normalize(vZ);
			vY = Vector3.Cross(vZ, vX);

			orientation.c0.x = vX.x;
			orientation.c0.y = vY.x;
			orientation.c0.z = vZ.x;
			orientation.c1.x = vX.y;
			orientation.c1.y = vY.y;
			orientation.c1.z = vZ.y;
			orientation.c2.x = vX.z;
			orientation.c2.y = vY.z;
			orientation.c2.z = vZ.z;

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
