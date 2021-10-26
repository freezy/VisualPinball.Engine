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
using Unity.Entities;
using Unity.Mathematics;
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
		private readonly VisualPinballSimulationSystemGroup _simulationSystemGroup;

		public event EventHandler Init;

		internal TeleporterApi(GameObject go, Player player)
		{
			_component = go.GetComponentInChildren<TeleporterComponent>();
			_player = player;
			_simulationSystemGroup = World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();
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

			var ballInSrc = _fromKicker.HasBall();
			var ballInDst = _toKicker.HasBall();
			if (!ballInSrc && !ballInDst || ballInSrc && ballInDst) {
				// duh, do nothing.
				return;
			}

			if (ballInDst) {
				Eject();
				return;
			}

			var ballData = _fromKicker.GetBallData();
			_fromKicker.DestroyBall();
			_toKicker.CreateSizedBallWithMass(ballData.Radius, ballData.Mass);

			// if no eject, we're done here.
			if (!_component.EjectAfterTeleportation) {
				return;
			}

			Eject();
		}

		private void Eject()
		{
			if (_component.EjectDelay > 0) {
				_simulationSystemGroup.ScheduleAction((int)math.round(_component.EjectDelay * 1000f), TriggerEjectCoil);

			} else {
				TriggerEjectCoil();
			}
		}

		private void TriggerEjectCoil()
		{
			if (!string.IsNullOrEmpty(_component.ToKickerItem)) {
				var kickerCoil = (_toKicker as IApiCoilDevice).Coil(_component.ToKickerItem);
				kickerCoil.OnCoil(true);
			}
		}

		void IApi.OnDestroy()
		{
		}
	}
}

