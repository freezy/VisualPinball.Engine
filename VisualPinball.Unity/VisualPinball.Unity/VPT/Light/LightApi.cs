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
using Light = VisualPinball.Engine.VPT.Light.Light;

namespace VisualPinball.Unity
{
	public class LightApi : ItemApi<LightAuthoring, Light, LightData>, IApiInitializable, IApiLamp
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
		private readonly LightAuthoring _lightAuthoring;

		void IApiWireDest.OnChange(bool enabled) => Set(
			enabled ? LightStatus.LightStateOn : LightStatus.LightStateOff,
			enabled ? 1.0f : 0f);

		public Color Color { get => _lightAuthoring.Color; set => _lightAuthoring.Color = value; }

		void IApiLamp.OnLamp(float value, ColorChannel channel)
		{
			switch (channel) {
				case ColorChannel.Alpha: {
					Set(value == 0.0f ? LightStatus.LightStateOff : LightStatus.LightStateOn, value);
					break;
				}
				case ColorChannel.Red: {
					var color = _lightAuthoring.Color;
					color.r = value;
					_lightAuthoring.Color = color;
					break;
				}
				case ColorChannel.Green: {
					var color = _lightAuthoring.Color;
					color.g = value;
					_lightAuthoring.Color = color;
					break;
				}
				case ColorChannel.Blue: {
					var color = _lightAuthoring.Color;
					color.b = value;
					_lightAuthoring.Color = color;
					break;
				}
				default:
					throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
			}
		}

		void IApiLamp.OnLampColor(Color color)
		{
			_lightAuthoring.Color = color;
		}

		internal LightApi(Light item, GameObject go, Player player) : base(go, player)
		{
			_lightAuthoring = go.GetComponentInChildren<LightAuthoring>();
			_state = item.Data.State;
		}

		private void Set(int lightStatus, float value)
		{
			switch (lightStatus) {
				case LightStatus.LightStateOff: {
					if (Data.FadeSpeedDown > 0) {
						_lightAuthoring.FadeTo(Data.FadeSpeedDown, 0);

					} else {
						_lightAuthoring.Enabled = false;
					}
					break;
				}

				case LightStatus.LightStateOn: {
					if (Data.FadeSpeedUp > 0) {
						_lightAuthoring.FadeTo(Data.FadeSpeedUp, value);

					} else {
						_lightAuthoring.Enabled = true;
					}
					break;
				}

				case LightStatus.LightStateBlinking: {
					_lightAuthoring.StartBlinking();
					break;
				}

				default:
					throw new ArgumentOutOfRangeException();
			}
			_state = lightStatus;
		}

		#region Events

		void IApiInitializable.OnInit(BallManager ballManager)
		{
			base.OnInit(ballManager);
			Init?.Invoke(this, EventArgs.Empty);
		}

		#endregion
	}
}
