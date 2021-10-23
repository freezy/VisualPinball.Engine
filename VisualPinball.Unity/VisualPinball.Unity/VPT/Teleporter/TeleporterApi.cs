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

// ReSharper disable ConvertIfStatementToSwitchStatement

using System;
using Logger = NLog.Logger;
using NLog;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class TeleporterApi : IApi, IApiCoilDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly TeleporterComponent _component;
		private readonly Player _player;

		private DeviceCoil _teleporterCoil;
		private KickerApi _fromKicker;
		private KickerApi _toKicker;

		public event EventHandler Init;

		internal TeleporterApi(GameObject go, Player player)
		{
			_component = go.GetComponentInChildren<TeleporterComponent>();
			_player = player;
		}

		void IApi.OnInit(BallManager ballManager)
		{
			_teleporterCoil = new DeviceCoil(OnTeleport);
			_fromKicker = _player.TableApi.Kicker(_component.FromKicker);
			_toKicker = _player.TableApi.Kicker(_component.ToKicker);

			Init?.Invoke(this, EventArgs.Empty);
		}

		IApiCoil IApiCoilDevice.Coil(string _) => _teleporterCoil;

		private void OnTeleport()
		{
			if (_toKicker == null || _fromKicker == null) {
				Logger.Warn($"[teleporter {_component.name}] Cannot teleport due to missing kicker configuration.");
				return;
			}

			var ballInPortalA = _fromKicker.HasBall();
			var ballInPortalB = _toKicker.HasBall();
			if (ballInPortalA && ballInPortalB || !ballInPortalA && !ballInPortalB) {
				// duh, do nothing.
				return;
			}

			if (ballInPortalA) {
				var ballData = _fromKicker.GetBallData();
				_fromKicker.DestroyBall();
				_toKicker.CreateSizedBallWithMass(ballData.Radius, ballData.Mass);

				if (_component.KickAfterTeleportation && !string.IsNullOrEmpty(_component.ToKickerItem)) {
					var kickerCoil = (_toKicker as IApiCoilDevice).Coil(_component.ToKickerItem);
					kickerCoil.OnCoil(true);
				}

			} else {
				var ballData = _toKicker.GetBallData();
				_toKicker.DestroyBall();
				_fromKicker.CreateSizedBallWithMass(ballData.Radius, ballData.Mass);
			}
		}

		void IApi.OnDestroy()
		{
		}
	}
}

