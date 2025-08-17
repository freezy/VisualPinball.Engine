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

// ReSharper disable InconsistentNaming

using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class BallComponent : MonoBehaviour
	{
		public int Id => gameObject.GetInstanceID();
		public float Radius = 25;
		public float Mass = 1;
		public float3 Velocity;
		public bool IsFrozen;

		internal BallState CreateState()
		{
			var pos = transform.localPosition.TranslateToVpx();
			return new BallState {
				Id = Id,
				IsFrozen = IsFrozen,
				Position = new float3(pos.x, pos.y, math.round(pos.z*100000) / 100000),
				Radius = Radius,
				Mass = Mass,
				Velocity = Velocity,
				BallOrientation = float3x3.identity,
				BallOrientationForUnity = float3x3.identity,
				RingCounterOldPos = 0,
				AngularMomentum = float3.zero
			};
		}

		public void Move(BallState ball)
		{
			var ballTransform = transform;

			// if ball was destroyed, don't do anything.
			if (!ballTransform || !ballTransform.gameObject) {
				return;
			}

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

#if UNITY_EDITOR
		private PhysicsEngine _physicsEngine;
		private float4x4 _playfieldToWorld;

		private void Awake()
		{
			_physicsEngine = GetComponentInParent<PhysicsEngine>();
			_playfieldToWorld = GetComponentInParent<PlayfieldComponent>().transform.localToWorldMatrix;
			UnityEditor.SceneView.duringSceneGui += DrawPhysicsDebug;
		}

		private void OnDestroy()
		{
			UnityEditor.SceneView.duringSceneGui -= DrawPhysicsDebug;
		}

		private void DrawPhysicsDebug(UnityEditor.SceneView sceneView)
		{
			ref var ballState = ref _physicsEngine.BallState(Id);

			// velocity
			DrawArrow(
				_playfieldToWorld.MultiplyPoint(ballState.Position.TranslateToWorld()),
				_playfieldToWorld.MultiplyVector((ballState.Velocity * 10).TranslateToWorld()),
				Color.white,
				0.01f
			);

			// hit normal
			// DrawArrow(
			// 	_playfieldToWorld.MultiplyPoint(ballState.Position.TranslateToWorld()),
			// 	_playfieldToWorld.MultiplyVector((ballState.CollisionEvent.HitNormal * 100).TranslateToWorld()),
			// 	ballState.CollisionEvent.HitFlag ? Color.red : Color.yellow,
			// 	0.01f
			// );
		}

		private static void DrawArrow(Vector3 pos, Vector3 direction, Color color, float arrowHeadLength = 0.025f, float arrowHeadAngle = 20.0f)
		{
			if (direction == Vector3.zero) {
				return;
			}
			Debug.DrawRay(pos, direction, color);
			var right = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180+arrowHeadAngle,0) * new Vector3(0,0,1);
			var left = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180-arrowHeadAngle,0) * new Vector3(0,0,1);
			Debug.DrawRay(pos + direction, right * arrowHeadLength, color);
			Debug.DrawRay(pos + direction, left * arrowHeadLength, color);
		}
#endif
	}
}
