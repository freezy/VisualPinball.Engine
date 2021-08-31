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

using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine.InputSystem;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public class SwitchPlayer
	{
		/// <summary>
		/// Maps the switch component to the API class.
		/// </summary>
		private readonly Dictionary<ISwitchDeviceAuthoring, IApiSwitchDevice> _switchDevices = new Dictionary<ISwitchDeviceAuthoring, IApiSwitchDevice>();

		/// <summary>
		/// Maps the switch configuration ID to a switch status.
		/// </summary>
		private readonly Dictionary<string, IApiSwitchStatus> _switchStatuses = new Dictionary<string, IApiSwitchStatus>();

		/// <summary>
		/// Maps the input action to a list of switch statuses
		/// </summary>
		private readonly Dictionary<string, List<KeyboardSwitch>> _keySwitchAssignments = new Dictionary<string, List<KeyboardSwitch>>();

		private TableAuthoring _tableComponent;
		private IGamelogicEngine _gamelogicEngine;
		private InputManager _inputManager;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		internal Dictionary<string, bool> SwitchStatusesClosed
			=> _switchStatuses.ToDictionary(s => s.Key, s => s.Value.IsSwitchClosed);
		internal IApiSwitch Switch(ISwitchDeviceAuthoring component, string deviceSwitchId)
			=> _switchDevices.ContainsKey(component) ? _switchDevices[component].Switch(deviceSwitchId) : null;
		internal void RegisterSwitchDevice(ISwitchDeviceAuthoring component, IApiSwitchDevice switchDeviceApi)
			=> _switchDevices[component] = switchDeviceApi;
		public bool SwitchDeviceExists(ISwitchDeviceAuthoring component)
			=> _switchDevices.ContainsKey(component);

		public void Awake(TableAuthoring tableComponent, IGamelogicEngine gamelogicEngine, InputManager inputManager)
		{
			_tableComponent = tableComponent;
			_gamelogicEngine = gamelogicEngine;
			_inputManager = inputManager;
		}

		public void OnStart()
		{
			// hook-up game switches
			if (_gamelogicEngine != null) {

				var config = _tableComponent.MappingConfig;
				_keySwitchAssignments.Clear();
				foreach (var switchMapping in config.Switches) {
					switch (switchMapping.Source) {

						case ESwitchSource.Playfield: {

							// mapping values must be set
							if (switchMapping.Device == null || string.IsNullOrEmpty(switchMapping.DeviceSwitchId)) {
								Logger.Warn($"Ignoring unassigned device switch \"{switchMapping.Id}\".");
								break;
							}

							// check if device exists
							if (!_switchDevices.ContainsKey(switchMapping.Device)) {
								Logger.Error($"Unknown switch device \"{switchMapping.Device}\".");
								break;
							}

							var device = _switchDevices[switchMapping.Device];
							var deviceSwitch = device.Switch(switchMapping.DeviceSwitchId);
							if (deviceSwitch != null) {
								var switchStatus = deviceSwitch.AddSwitchDest(new SwitchConfig(switchMapping));
								_switchStatuses[switchMapping.Id] = switchStatus;

							} else {
								Logger.Error($"Unknown switch \"{switchMapping.DeviceSwitchId}\" in switch device \"{switchMapping.Device}\".");
							}

							break;
						}

						case ESwitchSource.InputSystem:
							if (!_keySwitchAssignments.ContainsKey(switchMapping.InputAction)) {
								_keySwitchAssignments[switchMapping.InputAction] = new List<KeyboardSwitch>();
							}
							var keyboardSwitch = new KeyboardSwitch(switchMapping.Id, switchMapping.IsNormallyClosed);
							_keySwitchAssignments[switchMapping.InputAction].Add(keyboardSwitch);
							_switchStatuses[switchMapping.Id] = keyboardSwitch;
							break;

						case ESwitchSource.Constant:
							_switchStatuses[switchMapping.Id] = new ConstantSwitch(switchMapping.Constant == SwitchConstant.Closed);
							break;

						default:
							Logger.Error($"Unknown switch source \"{switchMapping.Source}\".");
							break;
					}
				}

				if (_keySwitchAssignments.Count > 0) {
					_inputManager.Enable(HandleKeyInput);
				}
			}
		}

		private void HandleKeyInput(object obj, InputActionChange change)
		{
			switch (change) {
				case InputActionChange.ActionStarted:
				case InputActionChange.ActionCanceled:
					var action = (InputAction)obj;
					if (_keySwitchAssignments.ContainsKey(action.name)) {
						if (_gamelogicEngine != null) {
							foreach (var sw in _keySwitchAssignments[action.name]) {
								sw.IsSwitchEnabled = change == InputActionChange.ActionStarted;
								_gamelogicEngine.Switch(sw.SwitchId, sw.IsSwitchClosed);
							}
						}
					} else {
						Logger.Info($"Unmapped input command \"{action.name}\".");
					}
					break;
			}
		}

		public void OnDestroy()
		{
			if (_keySwitchAssignments.Count > 0) {
				_inputManager.Disable(HandleKeyInput);
			}
		}

		// remove all below
		internal void RegisterSwitch(string goName, IApiSwitch _)
		{
			throw new System.NotImplementedException();
		}
	}

	internal class KeyboardSwitch : IApiSwitchStatus
	{
		public readonly string SwitchId;
		private readonly bool _isNormallyClosed;

		public bool IsSwitchEnabled { get; set; }
		public bool IsSwitchClosed => _isNormallyClosed ? !IsSwitchEnabled : IsSwitchEnabled;

		public KeyboardSwitch(string switchId, bool normallyClosed)
		{
			SwitchId = switchId;
			_isNormallyClosed = normallyClosed;
		}
	}

	internal class ConstantSwitch : IApiSwitchStatus
	{
		public bool IsSwitchEnabled { get; }
		public bool IsSwitchClosed => IsSwitchEnabled;

		public ConstantSwitch(bool isSwitchClosed)
		{
			IsSwitchEnabled = isSwitchClosed;
		}
	}
}
