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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Light;
using Color = VisualPinball.Engine.Math.Color;
using Light = VisualPinball.Engine.VPT.Light.Light;

namespace VisualPinball.Unity
{
	public class LightApi : ItemApi<Light, LightData>, IApiInitializable, IApiLamp
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		public int State { get => _state; set => Set(value); }

		private int _state;
		private readonly LightAuthoring _lightAuthoring;

		void IApiWireDest.OnChange(bool enabled) => Set(enabled ? LightStatus.LightStateOn : LightStatus.LightStateOff);
		void IApiLamp.OnLamp(bool enabled) => Set(enabled ? LightStatus.LightStateOn : LightStatus.LightStateOff);
		void IApiLamp.OnLamp(float value)
		{
			throw new NotImplementedException();
		}
		void IApiLamp.OnLamp(bool enabled, Color color) => Set(enabled ? LightStatus.LightStateOn : LightStatus.LightStateOff, color);

		internal LightApi(Light item, GameObject go, Player player) : base(item, player)
		{
			_lightAuthoring = go.GetComponentInChildren<LightAuthoring>();
			_state = item.Data.State;
		}

		private void Set(int lightStatus, float value = 1f)
		{
			switch (lightStatus) {
				case LightStatus.LightStateOff: {
					if (Data.FadeSpeedDown > 0) {
						_lightAuthoring.FadeTo(Data.FadeSpeedDown, 0f);

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

		private void Set(int lightStatus, Color color)
		{
			_lightAuthoring.Color = color;
			Set(lightStatus);
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
