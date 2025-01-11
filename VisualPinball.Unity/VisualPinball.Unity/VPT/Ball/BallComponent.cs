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
			Debug.DrawRay(pos, direction, color);
			var right = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180+arrowHeadAngle,0) * new Vector3(0,0,1);
			var left = Quaternion.LookRotation(direction) * Quaternion.Euler(0,180-arrowHeadAngle,0) * new Vector3(0,0,1);
			Debug.DrawRay(pos + direction, right * arrowHeadLength, color);
			Debug.DrawRay(pos + direction, left * arrowHeadLength, color);
		}
#endif
	}
}
