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
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	public class ScoreMotorApi : IApi, IApiCoilDevice, IApiSwitchDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly ScoreMotorComponent _scoreMotorComponent;
		private readonly Player _player;

		public event EventHandler Init;

		IApiCoil IApiCoilDevice.Coil(string deviceItem) => Coil(deviceItem);
		IApiSwitch IApiSwitchDevice.Switch(string deviceItem) => Switch(deviceItem);

		public DeviceCoil StartCoil;
		private Dictionary<string, DeviceSwitch> _deviceSwitches = new Dictionary<string, DeviceSwitch>();

		private bool _running;
		private float _degreesPerSecond;
		private float _totalTime;
		private int _pos;

		private IApiCoil Coil(string deviceItem)
		{
			return deviceItem switch
			{
				ScoreMotorComponent.StartCoilItem => StartCoil,
				_ => throw new ArgumentException($"Unknown coil \"{deviceItem}\". Valid name is \"{ScoreMotorComponent.StartCoilItem}\".")
			};
		}

		public IApiSwitch Switch(string deviceItem)
		{
			if (_deviceSwitches.ContainsKey(deviceItem)) {
				return _deviceSwitches[deviceItem];
			}

			throw new ArgumentException($"Unknown device switch \"{deviceItem}\".");
		}

		internal ScoreMotorApi(GameObject go, Player player)
		{
			_scoreMotorComponent = go.GetComponentInChildren<ScoreMotorComponent>();
			_player = player;

		}

		void IApi.OnInit(BallManager ballManager)
		{
			StartCoil = new DeviceCoil(_player, OnStartCoilEnabled);

			_deviceSwitches.Clear();

			_deviceSwitches[ScoreMotorComponent.MotorRunningSwitchItem] = new DeviceSwitch(ScoreMotorComponent.MotorRunningSwitchItem, false, SwitchDefault.NormallyOpen, _player);

			foreach(var @switch in _scoreMotorComponent.Switches) {
				_deviceSwitches[@switch.SwitchId] = new DeviceSwitch(@switch.SwitchId, false, SwitchDefault.NormallyOpen, _player);
			}

			_running = false;

			_degreesPerSecond = _scoreMotorComponent.Degrees / (_scoreMotorComponent.Duration / 1000f);

			Init?.Invoke(this, EventArgs.Empty);
		}

		private void OnStartCoilEnabled()
		{
			if (_running) {
				Logger.Info($"OnStartCoilEnabled: {_scoreMotorComponent.name} - aleady running");
				return;
			}

			_totalTime = 0;
			_pos = 0;

			_running = true;

			_deviceSwitches[ScoreMotorComponent.MotorRunningSwitchItem].SetSwitch(true);

			Advance();

			_scoreMotorComponent.OnUpdate += OnUpdate;
		}

		private void OnUpdate(object sender, EventArgs eventArgs)
		{
			_totalTime += Time.deltaTime;

			int newPos = (int)(_degreesPerSecond * _totalTime);

			while (_pos <= newPos && _pos < _scoreMotorComponent.Degrees) {
				Advance();
			}

			if (_pos >= _scoreMotorComponent.Degrees) {
				_scoreMotorComponent.OnUpdate -= OnUpdate;

				_deviceSwitches[ScoreMotorComponent.MotorRunningSwitchItem].SetSwitch(false);

				_running = false;
			}
		}

		private void Advance()
		{
			foreach (var @switch in _scoreMotorComponent.Switches) {
				if (@switch.Type == ScoreMotorSwitchType.EnableBetween) {
					if (_pos == @switch.StartPos) {
						_deviceSwitches[@switch.SwitchId].ScheduleSwitch(true, 0);
					} else if (_pos == @switch.EndPos) {
						_deviceSwitches[@switch.SwitchId].ScheduleSwitch(false, 0);
					}
				}
				else if (@switch.Type == ScoreMotorSwitchType.EnableEvery) {
					if (_pos > 0) {
						if (_pos % @switch.Freq == 0) {
							_deviceSwitches[@switch.SwitchId].ScheduleSwitch(true, 0);
						}
						else if (_pos % @switch.Freq == @switch.Duration) {
							_deviceSwitches[@switch.SwitchId].ScheduleSwitch(false, 0);
						}	
					}
				}
			}

			_pos++;
		}

		void IApi.OnDestroy()
		{
			Logger.Info($"Destroying {_scoreMotorComponent.name}");
		}
	}
}

