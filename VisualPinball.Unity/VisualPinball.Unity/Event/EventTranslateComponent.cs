// Visual Pinball Engine
// Copyright (C) 2025 freezy and VPE Team
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

using UnityEngine;

namespace VisualPinball.Unity
{
	public class EventTransformComponent : MonoBehaviour
	{
		public bool TwoWay;
		public bool TranslateGlobally;

		public AnimationCurve EnterAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1f, 1);
		public AnimationCurve ExitAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1f, 1);

		public float EnterAnimationDurationSeconds = 0.3f;
		public float ExitAnimationDurationSeconds = 0.3f;

		public Vector3 Direction;

		private IWireableComponent _wireComponent;

		private Vector3 _initialPosition;
		private bool _isAnimating;
		private float _animationElapsed;
		private float _animationDuration;
		private AnimationCurve _animationCurve;

		#region Runtime

		private void Awake()
		{
			_wireComponent = GetComponentInParent<IWireableComponent>();
			_initialPosition = TranslateGlobally ? transform.position : transform.localPosition;
		}

		private void Start()
		{
			if (_wireComponent == null) {
				return;
			}

			foreach (var deviceItem in _wireComponent.AvailableWireDestinations) {
				if (_wireComponent is ICoilDeviceComponent coilDevice) {
					var coil = coilDevice.CoilDevice(deviceItem.Id);
					coil.CoilStatusChanged += OnCoilChanged;
				}
			}
		}

		private void OnDestroy()
		{
			if (_wireComponent == null) {
				return;
			}
			foreach (var deviceItem in _wireComponent.AvailableWireDestinations) {
				if (_wireComponent is ICoilDeviceComponent coilDevice) {
					var coil = coilDevice.CoilDevice(deviceItem.Id);
					coil.CoilStatusChanged -= OnCoilChanged;
				}
			}
		}

		#endregion

		private void OnCoilChanged(object sender, NoIdCoilEventArgs e)
		{
			if (e.IsEnergized) {
				OnEnter();
			} else if (TwoWay) {
				OnExit();
			}
		}

		private void OnEnter()
		{
			_animationElapsed = 0f;
			_animationDuration = EnterAnimationDurationSeconds;
			_animationCurve = EnterAnimationCurve;
			_isAnimating = true;
		}

		private void Update()
		{
			if (!_isAnimating) {
				return;
			}

			if (_animationDuration <= 0f) {
				ApplyTranslation(1f);
				FinishAnimation();
				return;
			}

			_animationElapsed += Time.deltaTime;
			if (_animationElapsed < _animationDuration) {
				var f = _animationCurve.Evaluate(_animationElapsed / _animationDuration);
				ApplyTranslation(f);
				return;
			}

			ApplyTranslation(1f);
			FinishAnimation();
		}

		private void ApplyTranslation(float factor)
		{
			if (TranslateGlobally) {
				transform.position = _initialPosition + Direction * factor;
			} else {
				transform.localPosition = _initialPosition + Direction * factor;
			}
		}

		private void FinishAnimation()
		{
			if (!TwoWay) {
				if (TranslateGlobally) {
					transform.position = _initialPosition;
				} else {
					transform.localPosition = _initialPosition;
				}
			}

			_isAnimating = false;
		}

		private void OnExit()
		{

		}
	}
}
