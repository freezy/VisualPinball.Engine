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

using System.Collections;
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
		private Coroutine _enterCoroutine;

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
			if (_enterCoroutine != null) {
				StopCoroutine(_enterCoroutine);
			}
			_enterCoroutine = StartCoroutine(Animate(EnterAnimationDurationSeconds, EnterAnimationCurve));
		}

		private IEnumerator Animate(float duration, AnimationCurve curve)
		{
			var t = 0f;
			while (t < duration) {
				var f = curve.Evaluate(t / duration);

				if (TranslateGlobally) {
					transform.position = _initialPosition + Direction * f;
				} else {
					transform.localPosition = _initialPosition + Direction * f;
				}
				t += Time.deltaTime;
				yield return null;                               // wait one frame
			}

			if (!TwoWay) {
				if (TranslateGlobally) {
					transform.position = _initialPosition;
				} else {
					transform.localPosition = _initialPosition;
				}
			}
			_enterCoroutine = null;
		}

		private void OnExit()
		{

		}
	}
}
