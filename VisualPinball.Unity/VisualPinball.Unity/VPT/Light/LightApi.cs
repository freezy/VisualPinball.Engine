// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Light;
using Color = UnityEngine.Color;

namespace VisualPinball.Unity
{
	public class LightApi : ItemApi<LightComponent, LightData>,
		IApi, IApiLamp, IApiWireDeviceDest
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		public int State {
			get => _state;
			set => Set(value, value == LightStatus.LightStateOn ? 1.0f : 0f);
		}

		private int _state;
		private readonly LightComponent _lightComponent;
		private bool _initialized;

		void IApiWireDest.OnChange(bool enabled) => Set(
			enabled ? LightStatus.LightStateOn : LightStatus.LightStateOff,
			enabled ? 1.0f : 0f);

		public Color Color { get => _lightComponent.Color; set => _lightComponent.Color = value; }

		IApiWireDest IApiWireDeviceDest.Wire(string deviceItem) => this;

		void IApiLamp.OnLamp(float value, ColorChannel channel)
		{
			if (!_initialized) {
				// might have disabled some lights in the editor..
				return;
			}
			switch (channel) {
				case ColorChannel.Alpha: {
					Set(value == 0.0f ? LightStatus.LightStateOff : LightStatus.LightStateOn, value);
					break;
				}
				case ColorChannel.Red: {
					var color = _lightComponent.Color;
					color.r = value;
					_lightComponent.Color = color;
					break;
				}
				case ColorChannel.Green: {
					var color = _lightComponent.Color;
					color.g = value;
					_lightComponent.Color = color;
					break;
				}
				case ColorChannel.Blue: {
					var color = _lightComponent.Color;
					color.b = value;
					_lightComponent.Color = color;
					break;
				}
				default:
					throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
			}
		}

		void IApiLamp.OnLampColor(Color color)
		{
			_lightComponent.Color = color;
		}

		internal LightApi(GameObject go, Player player) : base(go, player)
		{
			_lightComponent = go.GetComponentInChildren<LightComponent>();
			_state = _lightComponent.State;
		}

		private void Set(int lightStatus, float value)
		{
			switch (lightStatus) {
				case LightStatus.LightStateOff: {
					_lightComponent.FadeTo(0);
					break;
				}

				case LightStatus.LightStateOn: {
					_lightComponent.FadeTo(value);

					break;
				}

				case LightStatus.LightStateBlinking: {
					_lightComponent.StartBlinking();
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			_state = lightStatus;
		}

		#region Events

		void IApi.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);
			_initialized = true;
		}

		void IApi.OnDestroy()
		{
		}

		#endregion
	}
}
