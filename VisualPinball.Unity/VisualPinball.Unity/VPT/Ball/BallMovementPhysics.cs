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

using UnityEngine;

namespace VisualPinball.Unity
{
	internal static class BallMovementPhysics
	{

		// protected override void OnStartRunning()
		// {
		// 	var root = Object.FindObjectOfType<PlayfieldComponent>();
		// 	var ltw = root.gameObject.transform.localToWorldMatrix;
		// 	_baseTransform = new float4x4(
		// 		ltw.m00, ltw.m01, ltw.m02, ltw.m03,
		// 		ltw.m10, ltw.m11, ltw.m12, ltw.m13,
		// 		ltw.m20, ltw.m21, ltw.m22, ltw.m23,
		// 		ltw.m30, ltw.m31, ltw.m32, ltw.m33
		// 	);
		// 	_player = Object.FindObjectOfType<Player>();
		// }

		public static void Move(BallState ball, Transform ballTransform)
		{
			// calculate/adapt height of ball
			var zHeight = !ball.IsFrozen ? ball.Position.z : ball.Position.z - ball.Radius;
			ballTransform.localPosition = Physics.TranslateToWorld(ball.Position.x, ball.Position.y, zHeight);
		
			var or = ball.BallOrientationForUnity;
		
			var vpX = new Vector3(or.c0.x, or.c1.x, or.c2.x);
			var vpY = new Vector3(or.c0.y, or.c1.y, or.c2.y);
			var vpZ = new Vector3(or.c0.z, or.c1.z, or.c2.z);

			// Debug.Log("c0: (" + or.c0.x + ", " + or.c0.y + ", " + or.c0.z + ")");
			// Debug.Log("c1: (" + or.c1.x + ", " + or.c1.y + ", " + or.c1.z + ")");
			// Debug.Log("c2: (" + or.c2.x + ", " + or.c2.y + ", " + or.c2.z + ")");

			// for security reasons, so that we don't get NaN, NaN, NaN, NaN erroro, when vectors are not fully orthonormalized because of skewMatrix operation
			Vector3.OrthoNormalize(ref vpZ, ref vpY, ref vpX);

			Quaternion q = Quaternion.LookRotation(vpZ, vpY);
		
			// flip Z axis
			q = FlipZAxis(q);
		
			ballTransform.localRotation = q.RotateToWorld();

			static Quaternion FlipZAxis(Quaternion q)
			{
				// which actually flips x and y axis visually...
				return new Quaternion(q.x, q.y, -q.z, -q.w);
			}

			/*
			 * I let these two in here, just in case we need them.
			static float3x3 transpose(float3x3 or)
			{
				float3x3 or2;
				or2.c0.x = or.c0.x;
				or2.c0.y = or.c1.x;
				or2.c0.z = or.c2.x;
				or2.c1.x = or.c0.y;
				or2.c1.y = or.c1.y;
				or2.c1.z = or.c2.y;
				or2.c2.x = or.c0.z;
				or2.c2.y = or.c1.z;
				or2.c2.z = or.c2.z;
				return or2;
			}
			static Quaternion QuaternionFromMatrix(Matrix4x4 m)
			{
				// Adapted from: http://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/index.htm
				Quaternion q = new Quaternion();
				q.w = Mathf.Sqrt(Mathf.Max(0, 1 + m[0, 0] + m[1, 1] + m[2, 2])) / 2;
				q.x = Mathf.Sqrt(Mathf.Max(0, 1 + m[0, 0] - m[1, 1] - m[2, 2])) / 2;
				q.y = Mathf.Sqrt(Mathf.Max(0, 1 - m[0, 0] + m[1, 1] - m[2, 2])) / 2;
				q.z = Mathf.Sqrt(Mathf.Max(0, 1 - m[0, 0] - m[1, 1] + m[2, 2])) / 2;
				q.x *= Mathf.Sign(q.x * (m[2, 1] - m[1, 2]));
				q.y *= Mathf.Sign(q.y * (m[0, 2] - m[2, 0]));
				q.z *= Mathf.Sign(q.z * (m[1, 0] - m[0, 1]));
				return q;
			}
			*/
		}
	}
}
