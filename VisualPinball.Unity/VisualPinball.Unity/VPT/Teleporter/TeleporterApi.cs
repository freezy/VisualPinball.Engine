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

		private readonly TeleporterComponent _teleporterComponent;
		private readonly Player _player;

		private DeviceCoil _teleporterCoil;
		private KickerApi _portalA;
		private KickerApi _portalB;

		public event EventHandler Init;

		internal TeleporterApi(GameObject go, Player player)
		{
			_teleporterComponent = go.GetComponentInChildren<TeleporterComponent>();
			_player = player;
		}

		void IApi.OnInit(BallManager ballManager)
		{
			_teleporterCoil = new DeviceCoil(OnTeleport);
			_portalA = _player.TableApi.Kicker(_teleporterComponent.FromKicker);
			_portalB = _player.TableApi.Kicker(_teleporterComponent.ToKicker);

			Init?.Invoke(this, EventArgs.Empty);
		}

		IApiCoil IApiCoilDevice.Coil(string _) => _teleporterCoil;

		private void OnTeleport()
		{
			var ballInPortalA = _portalA.HasBall();
			var ballInPortalB = _portalB.HasBall();
			if (ballInPortalA && ballInPortalB || !ballInPortalA && !ballInPortalB) {
				// duh, do nothing.
				return;
			}

			if (ballInPortalA) {
				var ballData = _portalA.GetBallData();
				_portalA.DestroyBall();
				_portalB.CreateSizedBallWithMass(ballData.Radius, ballData.Mass);

			} else {
				var ballData = _portalB.GetBallData();
				_portalB.DestroyBall();
				_portalA.CreateSizedBallWithMass(ballData.Radius, ballData.Mass);
			}
		}

		void IApi.OnDestroy()
		{
		}
	}
}

