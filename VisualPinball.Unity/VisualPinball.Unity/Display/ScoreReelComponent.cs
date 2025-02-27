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

using System.Collections;
using UnityEngine;

namespace VisualPinball.Unity
{
	[PackAs("ScoreReel")]
	[AddComponentMenu("Pinball/Display/Score Reel")]
	public class ScoreReelComponent : MonoBehaviour, IPackable
	{
		public enum ScoreReelDirection
		{
			Up, Down
		}

		#region Data

		[Tooltip("In which direction the reel rotates, when looking from the front.")]
		public ScoreReelDirection Direction = ScoreReelDirection.Down;

		[HideInInspector]
		public float Speed = 1;

		[HideInInspector]
		public float Wait;

		#endregion

		#region Packaging

		public byte[] Pack() => ScoreReelPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackagedRefs refs, PackagedFiles files) => null;

		public void Unpack(byte[] bytes) => ScoreReelPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackagedRefs refs, PackagedFiles files) { }

		#endregion

		/// <summary>
		/// True if the co-routine is running, false otherwise.
		/// </summary>
		private bool _isRunning;

		/// <summary>
		/// The position the reel is currently moving towards, or has already been moved to.
		/// </summary>
		private int _endPosition;

		/// <summary>
		/// The current rotation of the reel, in degrees.
		/// </summary>
		private float _currentRotation;

		/// <summary>
		/// The current position, based on the rotation of the reel.
		/// </summary>
		private int _currentPosition;

		private bool _isRotatingDown => Direction == ScoreReelDirection.Down;

		public void AnimateTo(int position)
		{
			var increasePositions = (position - _endPosition + 10) % 10;
			if (increasePositions == 0) { // early out if no additional increments.
				return;
			}
			_endPosition = position;

			if (!_isRunning) {
				_isRunning = true;
				StartCoroutine(nameof(Rotate));
			}
		}

		private IEnumerator Rotate()
		{
			while (_currentPosition != _endPosition) {
				var nextRotationSinceLastFrame = _currentRotation + Time.deltaTime * Speed * 36f;
				var nextPositionSinceLastFrame = Position(nextRotationSinceLastFrame);
				var numPositionsSinceLastFrame = nextPositionSinceLastFrame - _currentPosition;

				// check if since last frame we would over rotate to the wrong position
				if (_currentPosition < _endPosition && _currentPosition + numPositionsSinceLastFrame > _endPosition) {

					_currentRotation = _endPosition * 36f;
					_currentPosition = _endPosition;
					RotateReel();
					yield return new WaitForSeconds(Wait / 1000f);

				} else if (nextPositionSinceLastFrame != _currentPosition) {
					// if we reached a new position, click to position, and wait
					_currentRotation = ClickToRotation(nextRotationSinceLastFrame);
					_currentPosition = Position(_currentRotation);
					RotateReel();
					yield return new WaitForSeconds(Wait / 1000f);

				} else {
					// otherwise, continue animating
					_currentRotation = nextRotationSinceLastFrame % 360f;
					RotateReel();
					yield return null;
				}
			}
			_isRunning = false;
		}

		private void RotateReel() => transform.localRotation = Quaternion.Euler(0, 0,  _isRotatingDown ? _currentRotation : -_currentRotation);

		private static int Position(float rotation) => (int)(rotation / 36f);

		private static float ClickToRotation(float rotation) => (int)(rotation / 36f) * 36f % 360f;
	}
}
