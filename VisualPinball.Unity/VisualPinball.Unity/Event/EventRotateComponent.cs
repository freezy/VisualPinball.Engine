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

using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class EventRotateComponent : MonoBehaviour
	{
		public string EventName;
		public Vector3 Axis;
		public bool RotateGlobally;

		public float Factor = 1f;
		public float Offset;

		private IPinballEventEmitter _eventEmitter;
		private Quaternion _initialRotation;

		#region Runtime

		private void Awake()
		{
			var eventEmittersComponents = GetComponents<IPinballEventEmitter>();
			if (eventEmittersComponents != null) {
				foreach (var eventEmitter in eventEmittersComponents) {
					if (eventEmitter.Events.Select(e => e.Name).Contains(EventName)) {
						_eventEmitter = eventEmitter;
						break;
					}
				}
			}
			_initialRotation = RotateGlobally ? transform.rotation : transform.localRotation;
		}

		private void Start()
		{
			if (_eventEmitter == null) {
				return;
			}
			_eventEmitter.OnPinballEvent += OnPinballEvent;
		}

		private void OnPinballEvent(object sender, PinballEventArgs e)
		{
			if (e.Name != EventName) {
				return;
			}
			Rotate(e.Value, e.Unit);
		}

		private void Rotate(float value, PinballEventUnit unit)
		{
			float angleDeg;
			switch (unit) {
				case PinballEventUnit.Degrees:
					angleDeg = value;
					break;
				case PinballEventUnit.Radians:
					angleDeg = math.degrees(value);
					break;
				default:
					Debug.Log("Unsupported rotation unit: " + unit);
					return;
			}
			var delta = Quaternion.AngleAxis(angleDeg * Factor + Offset, Axis);
			if (RotateGlobally) {
				transform.rotation = delta * _initialRotation;
			} else {
				transform.localRotation = delta * _initialRotation;
			}
		}

		private void OnDestroy()
		{
			if (_eventEmitter == null) {
				return;
			}
			_eventEmitter.OnPinballEvent -= OnPinballEvent;
		}

		#endregion
	}
}
