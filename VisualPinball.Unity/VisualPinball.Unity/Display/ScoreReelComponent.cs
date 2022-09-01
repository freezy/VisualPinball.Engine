// Visual Pinball Engine
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

// ReSharper disable InconsistentNaming

using System.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class ScoreReelComponent : MonoBehaviour
	{
		public enum ScoreReelDirection
		{
			Up, Down
		}

		public bool DebugPrint = false;

		[Tooltip("In which direction the reel rotates, when looking from the front.")]
		public ScoreReelDirection Direction = ScoreReelDirection.Down;

		[HideInInspector]
		public float Speed = 1;

		[HideInInspector]
		public float Wait;

		/// <summary>
		/// True if the co-routine is running, false otherwise.
		/// </summary>
		private bool _isRunning;

		/// <summary>
		/// The position the reel is currently moving towards, or has already been moved to.
		/// </summary>
		private int _endPosition;

		/// <summary>
		/// How many positions are left to reach _nextPosition.
		/// </summary>
		private int _remainingPositions;

		/// <summary>
		/// The current rotation of the reel, in degrees.
		/// </summary>
		private float _currentRotation;

		private bool _isRotatingDown => Direction == ScoreReelDirection.Down;

		public void AnimateTo(int position)
		{
			var increasePositions = (position - _endPosition + 10) % 10;
			if (increasePositions == 0) { // early out if no additional increments.
				return;
			}
			_remainingPositions = (_remainingPositions + increasePositions) % 10;
			_endPosition = position;

			if (DebugPrint) {
				Debug.Log($"[reel] --> New position: {position} ({_remainingPositions} remaining)");
			}

			if (!_isRunning) {
				_isRunning = true;
				StartCoroutine(nameof(Rotate));
			}
		}

		private IEnumerator Rotate()
		{
			var dir = _isRotatingDown ? 1 : -1;
			while (_remainingPositions > 0) {
				var currentPosition = (int)(_currentRotation / 36f);
				var nextRotationSinceLastFrame = _currentRotation + dir * Time.deltaTime * Speed * 36f;
				var nextPositionSinceLastFrame = (int)(nextRotationSinceLastFrame / 36f);
				var numPositionsSinceLastFrame = math.abs(nextPositionSinceLastFrame - currentPosition);

				// check if since last frame we would over rotate to the wrong position
				if (numPositionsSinceLastFrame > _remainingPositions) {

					_currentRotation = (currentPosition + dir * _remainingPositions * 36f) % 360f;
					_remainingPositions = 0;

					if (DebugPrint) {
						var nextPosition = (int)(_currentRotation / 36f);
						Debug.Log($"[reel] === OVER-ROTATION: {currentPosition} -> {nextPositionSinceLastFrame}, resetting to {nextPosition}");
					}

					transform.localRotation = Quaternion.Euler(0, 0, _currentRotation);
					yield return new WaitForSeconds(Wait / 1000f);

				} else if (nextPositionSinceLastFrame != currentPosition) {
					// if we reached a new position, click to position, and wait
					_currentRotation = (int)(nextRotationSinceLastFrame / 36f) * 36f % 360f;
					_remainingPositions -= numPositionsSinceLastFrame;

					// round to correct position
					if (DebugPrint) {
						var nextPosition = (int)(_currentRotation / 36f);
						Debug.Log($"[reel] <-- Rotated to {nextPosition} ({numPositionsSinceLastFrame} increased, {_remainingPositions} remaining)");
					}

					transform.localRotation = Quaternion.Euler(0, 0, _currentRotation);
					yield return new WaitForSeconds(Wait / 1000f);

				} else {
					// otherwise, continue animating
					_currentRotation = nextRotationSinceLastFrame % 360f;
					if (DebugPrint) {
						var nextPosition = _currentRotation / 36f;
						Debug.Log($"[reel] ... Animating to {(int)(nextPosition * 100f) / 100f} ({numPositionsSinceLastFrame} increased, {_remainingPositions} remaining)");
					}

					transform.localRotation = Quaternion.Euler(0, 0, _currentRotation);
					yield return null;
				}
			}
			if (DebugPrint) {
				Debug.Log($"[reel] --- Finished at {(int)(_currentRotation / 36f)}");
			}
			_isRunning = false;
		}
	}
}
