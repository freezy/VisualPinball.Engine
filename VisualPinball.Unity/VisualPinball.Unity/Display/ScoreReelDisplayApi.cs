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
using Logger = NLog.Logger;
using NLog;
using UnityEngine;

namespace VisualPinball.Unity
{
	public class ScoreReelDisplayApi : IApi, IApiCoilDevice, IApiSwitchDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly ScoreReelDisplayComponent _scoreReelDisplayComponent;
		private readonly Player _player;

		public event EventHandler Init;

		IApiCoil IApiCoilDevice.Coil(string deviceItem) => Coil(deviceItem);
		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => Switch(deviceItem);

		public DeviceCoil ResetCoil;
		public DeviceSwitch MotorRunningSwitch;
		public DeviceSwitch MotorStepSwitch;
		public DeviceSwitch MotorTurnSwitch;

		private IApiCoil Coil(string deviceItem)
		{
			return deviceItem switch
			{
				ScoreReelDisplayComponent.ResetCoilItem => ResetCoil,
				_ => throw new ArgumentException($"Unknown coil \"{deviceItem}\". Valid name is \"{ScoreReelDisplayComponent.ResetCoilItem}\".")
			};
		}

		public IApiSwitch Switch(string deviceItem)
		{
			return deviceItem switch
			{
				ScoreReelDisplayComponent.MotorRunningSwitchItem => MotorRunningSwitch,
				ScoreReelDisplayComponent.MotorStepSwitchItem => MotorStepSwitch,
				ScoreReelDisplayComponent.MotorTurnSwitchItem => MotorTurnSwitch,
				_ => throw new ArgumentException($"Unknown switch \"{deviceItem}\". "
					+ "Valid names are \"{ScoreReelDisplayComponent.MotorRunningSwitchItem}\", " 
					+ "\"{ScoreReelDisplayComponent.MotorStepSwitchItem}\", \"{ScoreReelDisplayComponent.MotorTurnSwitchItem}\".")
			};
		}

		internal ScoreReelDisplayApi(GameObject go, Player player)
		{
			_scoreReelDisplayComponent = go.GetComponentInChildren<ScoreReelDisplayComponent>();
			_player = player;
		}

		void IApi.OnInit(BallManager ballManager)
		{
			ResetCoil = new DeviceCoil(_player, OnResetCoilEnabled);

			MotorRunningSwitch = new DeviceSwitch(ScoreReelDisplayComponent.MotorRunningSwitchItem, false, SwitchDefault.NormallyOpen, _player);
			MotorStepSwitch = new DeviceSwitch(ScoreReelDisplayComponent.MotorStepSwitchItem, true, SwitchDefault.NormallyOpen, _player);
			MotorTurnSwitch = new DeviceSwitch(ScoreReelDisplayComponent.MotorTurnSwitchItem, true, SwitchDefault.NormallyOpen, _player);

			Init?.Invoke(this, EventArgs.Empty);
		}

		private void OnResetCoilEnabled()
		{
		}

		void IApi.OnDestroy()
		{
			Logger.Info($"Destroying {_scoreReelDisplayComponent.name}");
		}
	}
}

