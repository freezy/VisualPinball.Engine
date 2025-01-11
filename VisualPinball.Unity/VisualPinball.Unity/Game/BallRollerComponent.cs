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
using UnityEngine.InputSystem;

namespace VisualPinball.Unity
{
	public class BallRollerComponent : MonoBehaviour
	{
		private PhysicsEngine _physicsEngine;
		private PlayfieldComponent _playfield;
		private Matrix4x4 _ltw;
		private Matrix4x4 _wtl;

		private Plane _playfieldPlane;

		private int _ballId = 0;

		private void Awake()
		{
			_playfield = GetComponentInChildren<PlayfieldComponent>();

			_ltw = Physics.VpxToWorld;
			_wtl = Physics.WorldToVpx;

			var p1 = _ltw.MultiplyPoint(new Vector3(-100f, 100f, 0));
			var p2 = _ltw.MultiplyPoint(new Vector3(100f, 100f, 0));
			var p3 = _ltw.MultiplyPoint(new Vector3(100f, -100f, 0));
			_playfieldPlane.Set3Points(p1, p2, p3);
			_physicsEngine = GetComponentInChildren<PhysicsEngine>();
		}

		private void Update()
		{
			if (Camera.main == null || _playfield == null) {
				return;
			}

			// find nearest ball
			if (Mouse.current.middleButton.wasPressedThisFrame) {
				if (GetCursorPositionOnPlayfield(out var mousePosition)) {
					var nearestDistance = float.PositiveInfinity;
					BallState nearestBall = default;
					var ballFound = false;

					using (var enumerator = _physicsEngine.Balls.GetEnumerator()) {
						while (enumerator.MoveNext()) {
							var ball = enumerator.Current.Value;

							if (ball.IsFrozen) {
								continue;
							}
							var distance = math.distance(mousePosition, ball.Position.xy);
							if (distance < nearestDistance) {
								nearestDistance = distance;
								nearestBall = ball;
								ballFound = true;
								_ballId = ball.Id;
							}
						}
					}

					if (ballFound) {
						UpdateBall(ref nearestBall, mousePosition);
					}
				}

			} else if (Mouse.current.middleButton.isPressed && _ballId != 0) {
				if (GetCursorPositionOnPlayfield(out var mousePosition)) {
					ref var ball = ref _physicsEngine.BallState(_ballId);
					UpdateBall(ref ball, mousePosition);
				}
			}

			if (Mouse.current.middleButton.wasReleasedThisFrame && _ballId != 0) {
				ref var ballData = ref _physicsEngine.BallState(_ballId);
				ballData.ManualControl = false;
				_ballId = 0;
			}
		}

		private void UpdateBall(ref BallState ballState, float2 position)
		{
			ballState.ManualControl = true;
			ballState.ManualPosition = position;
		}

		private bool GetCursorPositionOnPlayfield(out float2 position)
		{
			if (Camera.main == null) {
				position = float2.zero;
				return false;
			}

			var mouseOnScreenPos = Mouse.current.position.ReadValue();
			var ray = Camera.main.ScreenPointToRay(mouseOnScreenPos);

			if (_playfieldPlane.Raycast(ray, out var enter)) {
				var playfieldPosWorld = _playfield.transform.localToWorldMatrix.inverse.MultiplyPoint(ray.GetPoint(enter));
				var playfieldPosLocal = _wtl.MultiplyPoint(playfieldPosWorld);

				position = new float2(playfieldPosLocal.x, playfieldPosLocal.y);

				// todo check playfield bounds
				return true;
			}
			position = float2.zero;
			return false;
		}
	}
}
