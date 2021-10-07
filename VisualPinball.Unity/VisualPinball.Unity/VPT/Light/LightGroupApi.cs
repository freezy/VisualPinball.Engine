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
using VisualPinball.Engine.Math;
using Color = UnityEngine.Color;

namespace VisualPinball.Unity
{
	public class LightGroupApi : IApi, IApiLamp, IApiWireDeviceDest
	{
		private readonly LightApi[] _apis;

		public LightGroupApi(LightApi[] apis)
		{
			_apis = apis;
		}

		#region IApi

		/// <summary>
		/// Event emitted when the table is started.
		/// </summary>
		public event EventHandler Init;

		void IApi.OnInit(BallManager ballManager)
		{
			Init?.Invoke(this, EventArgs.Empty);
		}

		void IApi.OnDestroy()
		{
		}

		#endregion

		#region IApiLamp

		public Color Color
		{
			get => _color;
			set {
				_color = value;
				Do<LightApi>(l => l.Color = value);
			}
		}

		private Color _color;

		void IApiWireDest.OnChange(bool enabled) => Do<IApiWireDest>(l => l.OnChange(enabled));

		void IApiLamp.OnLamp(float value, ColorChannel channel) => Do<IApiLamp>(l => l.OnLamp(value, channel));

		void IApiLamp.OnLampColor(Color color) => Do<IApiLamp>(l => l.OnLampColor(color));

		#endregion

		#region IApiWireDeviceDest

		IApiWireDest IApiWireDeviceDest.Wire(string deviceItem) => this;

		#endregion

		private void Do<T>(Action<T> action)
		{
			foreach (var api in _apis) {
				if (api is T t) {
					action(t);
				}
			}
		}
	}
}
