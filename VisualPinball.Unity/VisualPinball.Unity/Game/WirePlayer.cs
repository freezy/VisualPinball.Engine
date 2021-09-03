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

namespace VisualPinball.Unity
{
	public class WirePlayer
	{
		/// <summary>
		/// Maps the wire destination component to the wire destination API.
		/// </summary>
		private readonly Dictionary<ICoilDeviceAuthoring, IApiWireDeviceDest> _wireDevices = new Dictionary<ICoilDeviceAuthoring, IApiWireDeviceDest>();
		private readonly Dictionary<string, List<WireDestConfig>> _keyWireAssignments = new Dictionary<string, List<WireDestConfig>>();

		private TableAuthoring _tableComponent;
		private InputManager _inputManager;
		private SwitchPlayer _switchPlayer;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		internal IApiWireDeviceDest WireDevice(ICoilDeviceAuthoring c) => _wireDevices.ContainsKey(c) ? _wireDevices[c] : null;
		internal void RegisterWireDevice(ICoilDeviceAuthoring component, IApiWireDeviceDest wireDeviceApi) => _wireDevices[component] = wireDeviceApi;

		public void Awake(TableAuthoring tableComponent, InputManager inputManager, SwitchPlayer switchPlayer)
		{
			_tableComponent = tableComponent;
			_inputManager = inputManager;
			_switchPlayer = switchPlayer;
		}

		public void OnStart()
		{
			var config = _tableComponent.MappingConfig;
			_keyWireAssignments.Clear();
			foreach (var wireData in config.Wires) {
				AddWire(wireData);
			}

			_inputManager.Enable(HandleKeyInput);
		}

		internal void AddWire(WireMapping wireMapping, bool isDynamic = false)
		{
			switch (wireMapping.Source) {

				case SwitchSource.Playfield: {
					// mapping values must be set
					if (wireMapping.SourceDevice == null || string.IsNullOrEmpty(wireMapping.SourceDeviceItem)) {
						Logger.Warn($"Ignore wire \"{wireMapping.Description}\" with unset source.");
						break;
					}

					// check if device exists
					if (!_switchPlayer.SwitchDeviceExists(wireMapping.SourceDevice)) {
						Logger.Error($"Unknown wire switch device \"{wireMapping.SourceDevice}\".");
						break;
					}

					var deviceSwitch = _switchPlayer.Switch(wireMapping.SourceDevice, wireMapping.SourceDeviceItem);
					if (deviceSwitch != null) {
						deviceSwitch.AddWireDest(new WireDestConfig(wireMapping) { IsDynamic = isDynamic });
						Logger.Info($"Wiring device switch \"{wireMapping.Src}\" to \"{wireMapping.Dst}\"");

					} else {
						Logger.Warn($"Unknown switch \"{wireMapping.Src}\" to wire to \"{wireMapping.Dst}\".");
					}
					break;
				}

				case SwitchSource.InputSystem: {
					if (!_keyWireAssignments.ContainsKey(wireMapping.SourceInputAction)) {
						_keyWireAssignments[wireMapping.SourceInputAction] = new List<WireDestConfig>();
					}
					_keyWireAssignments[wireMapping.SourceInputAction].Add(new WireDestConfig(wireMapping) { IsDynamic = isDynamic });
					break;
				}

				case SwitchSource.Constant:
					break;

				default:
					Logger.Warn($"Unknown wire switch source \"{wireMapping.Source}\".");
					break;
			}
		}

		internal void RemoveWire(WireMapping wireMapping)
		{
			switch (wireMapping.Source) {

				case SwitchSource.Playfield: {
					// mapping values must be set
					if (wireMapping.SourceDevice == null || string.IsNullOrEmpty(wireMapping.SourceDeviceItem)) {
						break;
					}

					// check if device exists
					if (!_switchPlayer.SwitchDeviceExists(wireMapping.SourceDevice)) {
						Logger.Error($"Unknown wire switch device \"{wireMapping.SourceDevice}\".");
						break;
					}

					var deviceSwitch = _switchPlayer.Switch(wireMapping.SourceDevice, wireMapping.SourceDeviceItem);
					if (deviceSwitch != null) {
						deviceSwitch.RemoveWireDest(wireMapping.DestinationDeviceItem);

					} else {
						Logger.Warn($"Unknown switch \"{wireMapping.Src}\" to wire to \"{wireMapping.Dst}\".");
					}
					break;
				}

				case SwitchSource.InputSystem: {
					if (!_keyWireAssignments.ContainsKey(wireMapping.SourceInputAction)) {
						_keyWireAssignments[wireMapping.SourceInputAction] = new List<WireDestConfig>();
					}
					var assignment = _keyWireAssignments[wireMapping.SourceInputAction]
						.FirstOrDefault(a => a.IsDynamic && a.Device == wireMapping.DestinationDevice && a.DeviceItem == wireMapping.DestinationDeviceItem);
					_keyWireAssignments[wireMapping.SourceInputAction].Remove(assignment);
					break;
				}

				case SwitchSource.Constant:
					break;

				default:
					Logger.Warn($"Unknown wire switch source \"{wireMapping.Source}\".");
					break;
			}
		}

		private void HandleKeyInput(object obj, InputActionChange change)
		{
			switch (change) {
				case InputActionChange.ActionStarted:
				case InputActionChange.ActionCanceled:
					var action = (InputAction)obj;
					if (_keyWireAssignments != null && _keyWireAssignments.ContainsKey(action.name)) {
						foreach (var wireConfig in _keyWireAssignments[action.name]) {
							if (_wireDevices.ContainsKey(wireConfig.Device)) {
								var device = _wireDevices[wireConfig.Device];
								var wire = device.Wire(wireConfig.DeviceItem);
								if (wire != null) {
									wire.OnChange(change == InputActionChange.ActionStarted);
								} else {
									Logger.Warn($"Unknown wire \"{wireConfig.DeviceItem}\" in wire device \"{wireConfig.Device}\".");
								}
							}
						}
					}
					break;
			}
		}

		public void OnDestroy()
		{
			if (_keyWireAssignments.Count > 0) {
				_inputManager.Disable(HandleKeyInput);
			}
		}
	}

	public class WireDestConfig
	{
		public readonly ICoilDeviceAuthoring Device;
		public readonly string DeviceItem;
		public readonly int PulseDelay;
		public bool IsPulseSource;

		/// <summary>
		/// If the destination is dynamic, it means it was added during
		/// gameplay. MPF does this, and it's called a "hardware rule". We tag
		/// it as such here so we can filter when removing the wire.
		/// </summary>
		public bool IsDynamic;

		public WireDestConfig(WireMapping wireMapping)
		{
			Device = wireMapping.DestinationDevice;
			DeviceItem = wireMapping.DestinationDeviceItem;
			PulseDelay = wireMapping.PulseDelay;
			IsPulseSource = false;
			IsDynamic = false;
		}

		public WireDestConfig WithPulse(bool isPulseSource)
		{
			IsPulseSource = isPulseSource;
			return this;
		}
	}
}
