// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using Logger = NLog.Logger;
using NLog;
using UnityEngine;
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	public class DropTargetBankApi : IApi, IApiCoilDevice, IApiSwitchDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly DropTargetBankComponent _dropTargetBankComponent;
		private readonly Player _player;
		private readonly PhysicsEngine _physicsEngine;

		private readonly List<DropTargetApi> _dropTargetApis = new List<DropTargetApi>();
		public DeviceSwitch SequenceCompletedSwitch;
		public DeviceCoil ResetCoil;

		public event EventHandler Init;

		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => Switch(deviceItem);
		IApiCoil IApiCoilDevice.Coil(string deviceItem) => Coil(deviceItem);
		
		public IApiSwitch Switch(string deviceItem)
		{
			return deviceItem switch
			{
				DropTargetBankComponent.SequenceCompletedSwitchItem => SequenceCompletedSwitch,
				_ => throw new ArgumentException($"Unknown sequence completed switch \"{deviceItem}\". Valid name is \"{DropTargetBankComponent.SequenceCompletedSwitchItem}\".")
			};
		}

		private IApiCoil Coil(string deviceItem)
		{
			return deviceItem switch
			{
				DropTargetBankComponent.ResetCoilItem => ResetCoil,
				_ => throw new ArgumentException($"Unknown reset coil \"{deviceItem}\". Valid name is \"{DropTargetBankComponent.ResetCoilItem}\".")
			};
		}

		internal DropTargetBankApi(GameObject go, Player player, PhysicsEngine physicsEngine)
		{
			_dropTargetBankComponent = go.GetComponentInChildren<DropTargetBankComponent>();
			_player = player;
			_physicsEngine = physicsEngine;
		}

		void IApi.OnInit(BallManager ballManager)
		{
			SequenceCompletedSwitch = new DeviceSwitch(DropTargetBankComponent.SequenceCompletedSwitchItem, false, SwitchDefault.NormallyOpen, _player, _physicsEngine);
			ResetCoil = new DeviceCoil(_player, OnResetCoilEnabled);

			for (var index = 0; index < _dropTargetBankComponent.BankSize; index++) {
				var dropTargetApi = _player.TableApi.DropTarget(_dropTargetBankComponent.DropTargets[index]);
				dropTargetApi.Switch += OnSwitch;

				_dropTargetApis.Add(dropTargetApi);

			}

			Init?.Invoke(this, EventArgs.Empty);
		}

		private void OnSwitch(object sender, SwitchEventArgs e)
		{
			foreach (var dropTargetApi in _dropTargetApis) {
				if (!dropTargetApi.IsSwitchEnabled) {
					SequenceCompletedSwitch.SetSwitch(false);

					return;
				}
			}

			Logger.Info($"OnSwitch - {_dropTargetBankComponent.name} sequence completed");

			SequenceCompletedSwitch.SetSwitch(true);
		}

		private void OnResetCoilEnabled()
		{
			Logger.Info($"OnResetCoilEnabled - resetting {_dropTargetBankComponent.name}");

			foreach (var dropTargetApi in _dropTargetApis) {
				dropTargetApi.IsDropped = false;
			}
		}

		void IApi.OnDestroy()
		{
			foreach (var dropTargetApi in _dropTargetApis) {
				dropTargetApi.Switch -= OnSwitch;
			}

			Logger.Info($"Destroying {_dropTargetBankComponent.name}");
		}
	}
}
