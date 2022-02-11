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
using UnityEngine;

namespace VisualPinball.Unity
{
	public class ScoreReelComponent : MonoBehaviour
	{
		[HideInInspector]
		public float Speed = 1;

		[HideInInspector]
		public float Wait;

		private bool _running;
		private int _nextPosition;
		private int _remainingPositions;

		private float _currentRotation;

		public void AnimateTo(int position)
		{
			var numPositions = (position - _nextPosition + 10) % 10;
			_remainingPositions += numPositions;
			_nextPosition = position;
			if (!_running) {
				_running = true;
				StartCoroutine(nameof(Rotate));
			}
		}

		private IEnumerator Rotate()
		{
			while (_remainingPositions > 0) {
				var lastPosition = (int)(_currentRotation / 36f);
				_currentRotation += Time.deltaTime * Speed * 36f;
				var currentPosition = (int)(_currentRotation / 36f);
				_currentRotation %= 360f;

				if (currentPosition != lastPosition) {

					// stop on correct position
					_currentRotation -= _currentRotation % 36f;
					transform.localRotation = Quaternion.Euler(0, 0, _currentRotation);

					_remainingPositions--;

					yield return new WaitForSeconds(Wait / 1000f);

				} else {
					transform.localRotation = Quaternion.Euler(0, 0, _currentRotation);
					yield return null;
				}
			}
			_running = false;
		}
	}
}
