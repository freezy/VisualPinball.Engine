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
		private readonly Dictionary<string, IApiSwitchDevice> _switchDevices = new Dictionary<string, IApiSwitchDevice>();
		private readonly Dictionary<string, List<string>> _keySwitchAssignments = new Dictionary<string, List<string>>();

		private Table _table;
		private IGamelogicEngine _gamelogicEngine;
		private InputManager _inputManager;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		internal IApiSwitch Switch(string itemName) => _switches.ContainsKey(itemName) ? _switches[itemName] : null;
		internal IApiSwitch Switch(string device, string itemName) => _switchDevices.ContainsKey(device) ? _switchDevices[device].Switch(itemName) : null;
		internal void RegisterSwitch(IItem item, IApiSwitch switchApi) => _switches[item.Name] = switchApi;
		internal void RegisterSwitchDevice(IItem item, IApiSwitchDevice switchDeviceApi) => _switchDevices[item.Name] = switchDeviceApi;
		public void RegisterWire(MappingsWireData wireData) => _switches[wireData.SourcePlayfieldItem].AddWireDest(new WireDestConfig(wireData));
		public bool SwitchExists(string name) => _switches.ContainsKey(name);
		public bool SwitchDeviceExists(string name) => _switchDevices.ContainsKey(name);

		public void Awake(Table table, IGamelogicEngine gamelogicEngine, InputManager inputManager)
		{
			_table = table;
			_gamelogicEngine = gamelogicEngine;
			_inputManager = inputManager;
		}

		public void OnStart()
		{
			// hook-up game switches
			if (_gamelogicEngine is IGamelogicEngineWithSwitches) {

				var config = _table.Mappings;
				_keySwitchAssignments.Clear();
				foreach (var switchData in config.Data.Switches) {
					switch (switchData.Source) {

						case SwitchSource.Playfield
							when !string.IsNullOrEmpty(switchData.PlayfieldItem)
							     && _switches.ContainsKey(switchData.PlayfieldItem): {
							var element = _switches[switchData.PlayfieldItem];
							element.AddSwitchId(new SwitchConfig(switchData));
							break;
						}

						case SwitchSource.InputSystem:
							if (!_keySwitchAssignments.ContainsKey(switchData.InputAction)) {
								_keySwitchAssignments[switchData.InputAction] = new List<string>();
							}
							_keySwitchAssignments[switchData.InputAction].Add(switchData.Id);
							break;

						case SwitchSource.Playfield:
							Logger.Warn($"Cannot find switch \"{switchData.PlayfieldItem}\" on playfield!");
							break;

						case SwitchSource.Device
							when !string.IsNullOrEmpty(switchData.Device)
							     && _switchDevices.ContainsKey(switchData.Device): {
							var device = _switchDevices[switchData.Device];
							var deviceSwitch = device.Switch(switchData.DeviceItem);
							if (deviceSwitch != null) {
								deviceSwitch.AddSwitchId(new SwitchConfig(switchData));

							} else {
								Logger.Warn($"Unknown switch \"{switchData.DeviceItem}\" in switch device \"{switchData.Device}\".");
							}
							break;
						}
						case SwitchSource.Device when string.IsNullOrEmpty(switchData.Device):
							Logger.Warn($"Switch device not set for switch \"{switchData.Id}\".");
							break;

						case SwitchSource.Device when !_switchDevices.ContainsKey(switchData.Device):
							Logger.Warn($"Unknown switch device \"{switchData.Device}\" for switch \"{switchData.Id}\".");
							break;

						case SwitchSource.Constant:
							break;

						default:
							Logger.Warn($"Unknown switch source \"{switchData.Source}\".");
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
						if (_gamelogicEngine is IGamelogicEngineWithSwitches engineWithSwitches) {
							foreach (var switchId in _keySwitchAssignments[action.name]) {
								engineWithSwitches.Switch(switchId, change == InputActionChange.ActionStarted);
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
}
