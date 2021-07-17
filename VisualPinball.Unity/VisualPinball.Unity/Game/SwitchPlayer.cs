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
using VisualPinball.Engine.VPT.Mappings;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public class SwitchPlayer
	{
		private readonly Dictionary<string, IApiSwitch> _switches = new Dictionary<string, IApiSwitch>();
		private readonly Dictionary<string, IApiSwitchStatus> _switchStatuses = new Dictionary<string, IApiSwitchStatus>();
		private readonly Dictionary<string, IApiSwitchDevice> _switchDevices = new Dictionary<string, IApiSwitchDevice>();
		private readonly Dictionary<string, List<KeyboardSwitch>> _keySwitchAssignments = new Dictionary<string, List<KeyboardSwitch>>();

		private TableContainer _tableContainer;
		private IGamelogicEngine _gamelogicEngine;
		private InputManager _inputManager;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		internal Dictionary<string, bool> SwitchStatusesClosed => _switchStatuses.ToDictionary(s => s.Key, s => s.Value.IsSwitchClosed);

		internal IApiSwitch Switch(string itemName) => _switches.ContainsKey(itemName) ? _switches[itemName] : null;
		internal IApiSwitch Switch(string device, string itemName) => _switchDevices.ContainsKey(device) ? _switchDevices[device].Switch(itemName) : null;
		internal void RegisterSwitch(IItem item, IApiSwitch switchApi) => _switches[item.Name] = switchApi;
		internal void RegisterSwitchDevice(IItem item, IApiSwitchDevice switchDeviceApi) => _switchDevices[item.Name] = switchDeviceApi;
		public void RegisterWire(MappingsWireData wireData, bool isDynamic = false) => _switches[wireData.SourcePlayfieldItem].AddWireDest(new WireDestConfig(wireData) {IsDynamic = isDynamic});
		public void UnregisterWire(MappingsWireData wireData) => _switches[wireData.SourcePlayfieldItem].RemoveWireDest(wireData.DestinationId);
		public bool SwitchExists(string name) => _switches.ContainsKey(name);
		public bool SwitchDeviceExists(string name) => _switchDevices.ContainsKey(name);

		public void Awake(TableContainer tableContainer, IGamelogicEngine gamelogicEngine, InputManager inputManager)
		{
			_tableContainer = tableContainer;
			_gamelogicEngine = gamelogicEngine;
			_inputManager = inputManager;
		}

		public void OnStart()
		{
			// hook-up game switches
			if (_gamelogicEngine != null) {

				var config = _tableContainer.Mappings;
				_keySwitchAssignments.Clear();
				foreach (var switchData in config.Data.Switches) {
					switch (switchData.Source) {

						case SwitchSource.Playfield: {

							if (string.IsNullOrEmpty(switchData.PlayfieldItem)) {
								Logger.Warn($"Ignoring unassigned switch \"{switchData.Id}\".");
								break;
							}

							if (!_switches.ContainsKey(switchData.PlayfieldItem)) {
								Logger.Error($"Cannot find item \"{switchData.PlayfieldItem}\" for switch \"{switchData.Id}\".");
								break;
							}

							var element = _switches[switchData.PlayfieldItem];
							var switchStatus = element.AddSwitchDest(new SwitchConfig(switchData));
							_switchStatuses[switchData.Id] = switchStatus;
							break;
						}

						case SwitchSource.InputSystem:
							if (!_keySwitchAssignments.ContainsKey(switchData.InputAction)) {
								_keySwitchAssignments[switchData.InputAction] = new List<KeyboardSwitch>();
							}
							var keyboardSwitch = new KeyboardSwitch(switchData.Id, switchData.IsNormallyClosed);
							_keySwitchAssignments[switchData.InputAction].Add(keyboardSwitch);
							_switchStatuses[switchData.Id] = keyboardSwitch;
							break;

						case SwitchSource.Device: {

							// mapping values must be set
							if (string.IsNullOrEmpty(switchData.Device) || string.IsNullOrEmpty(switchData.DeviceItem)) {
								Logger.Warn($"Ignoring unassigned device switch \"{switchData.Id}\".");
								break;
							}

							// check if device exists
							if (!_switchDevices.ContainsKey(switchData.Device)) {
								Logger.Error($"Unknown switch device \"{switchData.Device}\".");
								break;
							}

							var device = _switchDevices[switchData.Device];
							var deviceSwitch = device.Switch(switchData.DeviceItem);
							if (deviceSwitch != null) {
								var switchStatus = deviceSwitch.AddSwitchDest(new SwitchConfig(switchData));
								_switchStatuses[switchData.Id] = switchStatus;

							} else {
								Logger.Error($"Unknown switch \"{switchData.DeviceItem}\" in switch device \"{switchData.Device}\".");
							}

							break;
						}

						case SwitchSource.Constant:
							_switchStatuses[switchData.Id] = new ConstantSwitch(switchData.Constant == SwitchConstant.Closed);
							break;

						default:
							Logger.Error($"Unknown switch source \"{switchData.Source}\".");
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
	}

	internal class KeyboardSwitch : IApiSwitchStatus
	{
		public readonly string SwitchId;
		public readonly bool IsNormallyClosed;

		public bool IsSwitchEnabled { get; set; }
		public bool IsSwitchClosed => IsNormallyClosed ? !IsSwitchEnabled : IsSwitchEnabled;

		public KeyboardSwitch(string switchId, bool normallyClosed)
		{
			SwitchId = switchId;
			IsNormallyClosed = normallyClosed;
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
