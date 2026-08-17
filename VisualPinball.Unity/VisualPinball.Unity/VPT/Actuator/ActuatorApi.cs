// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

using System;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class ActuatorApi : IApi, IApiCoilDevice, IApiWireDeviceDest, IApiCoil
	{
		private readonly ActuatorComponent _component;
		private bool _warnedBooleanFollowValue;

		public event EventHandler Init;
		public event EventHandler Reached;
		public event EventHandler<NoIdCoilEventArgs> CoilStatusChanged;

		internal ActuatorApi(GameObject go)
		{
			_component = go.GetComponent<ActuatorComponent>();
		}

		public float Position => _component.Position;
		public float TargetPosition => _component.TargetPosition;
		public bool IsMoving => _component.IsMoving;

		/// <summary>
		/// Gets or commands the active endpoint. A script command takes precedence until the next
		/// qualified coil edge; it does not rewrite the sampled physical coil state.
		/// </summary>
		public bool IsActive {
			get => TargetPosition >= 0.5f;
			set => _component.SetActive(value);
		}

		/// <summary>
		/// Toggles the commanded endpoint. A script command takes precedence until the next
		/// qualified coil edge; it does not rewrite the sampled physical coil state.
		/// </summary>
		public void Toggle() => _component.Toggle();

		/// <summary>
		/// Immediately restores or previews a normalized pose. A script command takes precedence
		/// until the next qualified coil edge; it does not rewrite the sampled physical coil state.
		/// </summary>
		public void SnapTo(float position) => _component.SnapTo(position);

		void IApi.OnInit(BallManager ballManager) => Init?.Invoke(this, EventArgs.Empty);
		void IApi.OnDestroy() { }

		IApiCoil IApiCoilDevice.Coil(string deviceItem) => Coil(deviceItem);
		public IApiWireDest Wire(string deviceItem) => Coil(deviceItem);

		private IApiCoil Coil(string deviceItem)
		{
			return deviceItem switch {
				ActuatorComponent.ActuatorCoilItem => this,
				_ => throw new ArgumentException($"Unknown actuator coil \"{deviceItem}\". Valid name is \"{ActuatorComponent.ActuatorCoilItem}\".")
			};
		}

		void IApiCoil.OnCoil(bool enabled) => ApplyCoilValue(enabled ? 1f : 0f);

		void IApiCoil.OnCoil(float value) => ApplyCoilValue(math.saturate(value));

		void IApiWireDest.OnChange(bool enabled)
		{
			if (_component.CoilMode == ActuatorCoilMode.FollowValue && !_warnedBooleanFollowValue) {
				_warnedBooleanFollowValue = true;
				Debug.LogWarning($"Actuator '{_component.name}' is configured to Follow Value but is receiving boolean wire input. Use a plain coil mapping to preserve proportional values.", _component);
			}
			ApplyCoilValue(enabled ? 1f : 0f);
		}

		internal void NotifyReached() => Reached?.Invoke(this, EventArgs.Empty);

		private void ApplyCoilValue(float value)
		{
			_component.ApplyCoilValue(value);
			CoilStatusChanged?.Invoke(this, new NoIdCoilEventArgs(value > 0f));
		}
	}
}
