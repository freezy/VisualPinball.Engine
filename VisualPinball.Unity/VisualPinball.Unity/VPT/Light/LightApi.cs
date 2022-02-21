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

using System;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT.Light;

namespace VisualPinball.Unity
{
	public class LightApi : ItemApi<LightComponent, LightData>, IApiLamp, IApiWireDeviceDest
	{
		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		private bool _initialized;
		private LampStatus _status;
		private float _intensity = 1f;

		private readonly LightComponent _lightComponent;

		void IApiWireDest.OnChange(bool enabled)
		{
			OnLamp(enabled ? LampStatus.On : LampStatus.Off);
		}

		public void OnLamp(LampStatus status)
		{
			if (!_initialized) {
				// might have disabled some lights in the editor..
				return;
			}
			_status = status;
			Update();
		}

		public void OnLamp(float intensity)
		{
			if (!_initialized) {
				// might have disabled some lights in the editor..
				return;
			}

			_intensity = intensity;
			Update();
		}

		private void Update()
		{
			switch (_status) {
				case LampStatus.Off: {
					_lightComponent.FadeTo(0);
					break;
				}
				case LampStatus.On: {
					_lightComponent.FadeTo(_intensity);
					break;
				}
				case LampStatus.Blinking: {
					_lightComponent.StartBlinking(_intensity);
					break;
				}
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public void OnLamp(Color color)
		{
			if (!_initialized) {
				// might have disabled some lights in the editor..
				return;
			}
			_lightComponent.Color = new Color(color.r, color.g, color.b, _lightComponent.Color.a);
		}

		IApiWireDest IApiWireDeviceDest.Wire(string deviceItem) => this;

		internal LightApi(GameObject go, Player player) : base(go, player)
		{
			_lightComponent = go.GetComponentInChildren<LightComponent>();
			_status = _lightComponent.State;
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
