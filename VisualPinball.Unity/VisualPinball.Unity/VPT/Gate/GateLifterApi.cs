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
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class GateLifterApi : IApi, IApiCoilDevice, IApiWireDeviceDest
	{
		public DeviceCoil LifterCoil;

		public event EventHandler Init;

		private readonly Player _player;
		private readonly GateComponent _gateComponent;
		private readonly GateLifterComponent _gateLifterComponent;
		private readonly GateColliderComponent _gateColliderComponent;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private GateApi _gateApi;

		internal GateLifterApi(GameObject go, Player player)
		{
			_gateComponent = go.GetComponent<GateComponent>();
			_gateColliderComponent = go.GetComponent<GateColliderComponent>();
			_gateLifterComponent = go.GetComponent<GateLifterComponent>();
			_player = player;
		}

		void IApi.OnInit(BallManager ballManager)
		{
			LifterCoil = new DeviceCoil(_player, OnLifterCoilEnabled, OnLifterCoilDisabled);
			_gateApi = _player.TableApi.Gate(_gateComponent);
			Init?.Invoke(this, EventArgs.Empty);
		}

		public IApiCoil Coil(string deviceItem)
		{
			return deviceItem switch {
				GateLifterComponent.LifterCoilItem => LifterCoil,
				_ => throw new ArgumentException($"Unknown coil \"{deviceItem}\". Valid name is \"{GateLifterComponent.LifterCoilItem}\".")
			};
		}

		public IApiWireDest Wire(string deviceItem)
		{
			return deviceItem switch {
				GateLifterComponent.LifterCoilItem => LifterCoil,
				_ => throw new ArgumentException($"Unknown wire \"{deviceItem}\". Valid name is \"{GateLifterComponent.LifterCoilItem}\".")
			};
		}

		private void OnLifterCoilEnabled()
		{
			if (_gateColliderComponent == null) {
				Logger.Warn("Lifter coil enabled, but gate collider not found.");
				return;
			}

			_gateApi.IsCollidable = false;
			_gateApi.Lift(_gateLifterComponent.AnimationSpeed, _gateLifterComponent.LiftedAngleDeg);
		}

		private void OnLifterCoilDisabled()
		{
			if (_gateColliderComponent == null) {
				Logger.Warn("Lifter coil enabled, but gate collider not found.");
				return;
			}

			_gateApi.IsCollidable = true;
			_gateApi.Lift(_gateLifterComponent.AnimationSpeed, 0f);
		}

		void IApi.OnDestroy()
		{
		}
	}
}
