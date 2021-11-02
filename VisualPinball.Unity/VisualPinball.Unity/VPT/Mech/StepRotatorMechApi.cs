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
using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class StepRotatorMechApi : IApi, IApiSwitchDevice, IApiCoilDevice
	{
		public event EventHandler Init;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private enum Direction
		{
			Forward = 0,
			Reverse = 1
		}

		private readonly Player _player;
		private readonly StepRotatorMechComponent _component;

		private DeviceCoil _motorCoil;
		private bool _enabled;
		private float _currentStep;
		private Direction _direction;

		private Dictionary<string, DeviceSwitch> _switches;
		private Dictionary<string, MechMark> _marks;

		internal StepRotatorMechApi(GameObject go, Player player)
		{
			_component = go.GetComponentInChildren<StepRotatorMechComponent>();
			_player = player;
		}


		void IApi.OnInit(BallManager ballManager)
		{
			_enabled = false;
			_currentStep = 0;
			_direction = Direction.Forward;

			_motorCoil = new DeviceCoil(OnMotorCoilEnabled, OnMotorCoilDisabled);

			_marks = _component.Marks.ToDictionary(m => m.SwitchId, m => m);
			_switches = _component.Marks.ToDictionary(m => m.SwitchId, m => new DeviceSwitch(m.SwitchId, false, SwitchDefault.NormallyOpen, _player));
			var i = 0;
			foreach (var sw in _switches.Values) {
				sw.SetSwitch(i == 0);
				i++;
			}

			Init?.Invoke(this, EventArgs.Empty);
		}

		IApiCoil IApiCoilDevice.Coil(string deviceItem) => Coil(deviceItem);

		private IApiCoil Coil(string deviceItem)
		{
			return deviceItem switch {
				StepRotatorMechComponent.MotorCoilItem => _motorCoil,
				_ => throw new ArgumentException($"Unknown coil \"{deviceItem}\". Valid name is: \"{StepRotatorMechComponent.MotorCoilItem}\".")
			};
		}

		IApiSwitch IApiSwitchDevice.Switch(string deviceItem)
		{
			if (!_switches.ContainsKey(deviceItem)) {
				throw new ArgumentException($"Unknown switch \"{deviceItem}\". Valid names are: [ \"{string.Join("\", ", _switches.Keys)}\" ].");
			}
			return _switches[deviceItem];
		}

		private void OnMotorCoilEnabled()
		{
			_player.OnUpdate += OnUpdate;
			_component.Target.StartRotating();
			_enabled = true;
			Logger.Info("OnGunMotorCoilEnabled - starting rotation");
		}

		private void OnMotorCoilDisabled()
		{
			_enabled = false;
			Logger.Info("OnGunMotorCoilDisabled - stopping rotation");
			_player.OnUpdate -= OnUpdate;
		}

		private void OnUpdate(object sender, EventArgs eventArgs)
		{
			if (!_enabled || _component.NumSteps == 0)
			{
				return;
			}

			var numSteps = _component.NumSteps;
			var speed = numSteps * 2 / 6.5f * Time.deltaTime;

			// determine position
			if (_direction == Direction.Forward)
			{
				_currentStep += speed;

				if (_currentStep >= numSteps)
				{
					_currentStep = numSteps - (_currentStep - numSteps);
					_direction = Direction.Reverse;
				}
			}
			else
			{
				_currentStep -= speed;

				if (_currentStep <= 0)
				{
					_currentStep = -_currentStep;
					_direction = Direction.Forward;
				}
			}

			// check if any mark hit
			foreach (var mark in _marks.Values) {
				var sw = _switches[mark.SwitchId];
				if (_currentStep >= mark.StepBeginning && _currentStep <= mark.StepEnd) {
					if (!sw.IsSwitchEnabled) {
						sw.SetSwitch(true);
					}

				} else if (sw.IsSwitchEnabled) {
					sw.SetSwitch(false);
				}
			}

			// rotate target
			_component.UpdateRotation(_currentStep / numSteps);
		}

		void IApi.OnDestroy()
		{
			Logger.Info($"Destroying {_component.name}");
		}
	}
}

