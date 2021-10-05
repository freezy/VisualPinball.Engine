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
		private readonly LightComponent[] _lightComponents;

		void IApiWireDest.OnChange(bool enabled) => Set(
			enabled ? LightStatus.LightStateOn : LightStatus.LightStateOff,
			enabled ? 1.0f : 0f);

		public Color Color { get => _lightComponents[0].Color; set => Do(l => l.Color = value); }

		IApiWireDest IApiWireDeviceDest.Wire(string deviceItem) => this;

		void IApiLamp.OnLamp(float value, ColorChannel channel)
		{
			switch (channel) {
				case ColorChannel.Alpha: {
					Set(value == 0.0f ? LightStatus.LightStateOff : LightStatus.LightStateOn, value);
					break;
				}
				case ColorChannel.Red:
					Do(l => {
						var color = l.Color;
						color.r = value;
						l.Color = color;
					});
					break;
				case ColorChannel.Green:
					Do(l => {
						var color = l.Color;
						color.g = value;
						l.Color = color;
					});
					break;
				case ColorChannel.Blue:
					Do(l => {
						var color = l.Color;
						color.b = value;
						l.Color = color;
					});
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
			}
		}

		void IApiLamp.OnLampColor(Color color)
		{
			Do(l => l.Color = color);
		}

		internal LightApi(GameObject go, Player player) : base(go, player)
		{
			_lightComponents = go.GetComponentsInChildren<LightComponent>();
			_state = _lightComponents[0].State;
		}

		private void Do(Action<LightComponent> action)
		{
			foreach (var t in _lightComponents) {
				action(t);
			}
		}

		private void Set(int lightStatus, float value)
		{
			switch (lightStatus) {
				case LightStatus.LightStateOff:
					Do(l => {
						if (l.FadeSpeedDown > 0) {
							l.FadeTo(0);

						} else {
							l.Enabled = false;
						}
					});
					break;
				case LightStatus.LightStateOn:
					Do(l => {
						if (l.FadeSpeedUp > 0) {
							l.FadeTo(value);

						} else {
							l.Enabled = true;
						}
					});
					break;

				case LightStatus.LightStateBlinking:
					Do(l => {
						l.StartBlinking();
					});
					break;

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
		}

		void IApi.OnDestroy()
		{
		}

		#endregion
	}
}
