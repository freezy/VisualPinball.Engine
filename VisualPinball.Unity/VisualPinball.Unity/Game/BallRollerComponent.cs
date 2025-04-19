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
	[PackAs("BallRoller")]
	public class BallRollerComponent : BallDebugComponent, IPackable
	{
		#region Packaging

		public byte[] Pack() => PackageApi.Packer.Empty;

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) { }

		public void UnpackReferences(byte[] bytes, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion

		private void Update()
		{
			if (!Camera.main || !_playfield || !_player) {
				return;
			}

			// find nearest ball
			if (Mouse.current.middleButton.wasPressedThisFrame) {
				if (GetCursorPositionOnPlayfield(out var mousePosition, out var _)) {

					if (_player.BallManager.FindNearest(mousePosition.xy, out var nearestBall)) {
						_ballId = nearestBall.Id;
						UpdateBall(ref nearestBall, mousePosition.xy);
					}
				}

			} else if (Mouse.current.middleButton.isPressed && _ballId != 0) {
				if (GetCursorPositionOnPlayfield(out var mousePosition, out var _)) {
					ref var ball = ref _physicsEngine.BallState(_ballId);
					UpdateBall(ref ball, mousePosition.xy);
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
	}
}
