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
	public class WirePlayer
	{
		private readonly Dictionary<string, IApiWireDest> _wires = new Dictionary<string, IApiWireDest>();
		private readonly Dictionary<string, IApiWireDeviceDest> _wireDevices = new Dictionary<string, IApiWireDeviceDest>();
		private readonly Dictionary<string, List<WireDestConfig>> _keyWireAssignments = new Dictionary<string, List<WireDestConfig>>();


		private Table _table;
		private InputManager _inputManager;
		private SwitchPlayer _switchPlayer;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		internal IApiWireDest Wire(string n) => _wires.ContainsKey(n) ? _wires[n] : null;
		internal IApiWireDeviceDest WireDevice(string n) => _wireDevices.ContainsKey(n) ? _wireDevices[n] : null;
		internal void RegisterWire(IItem item, IApiWireDest wireApi) => _wires[item.Name] = wireApi;
		internal void RegisterWireDevice(IItem item, IApiWireDeviceDest wireDeviceApi) => _wireDevices[item.Name] = wireDeviceApi;

		public void Awake(Table table, InputManager inputManager, SwitchPlayer switchPlayer)
		{
			_table = table;
			_inputManager = inputManager;
			_switchPlayer = switchPlayer;
		}

		public void OnStart()
		{
			var config = _table.Mappings;
			_keyWireAssignments.Clear();
			foreach (var wireData in config.Data.Wires) {
				switch (wireData.Source) {

					case SwitchSource.Playfield
						when !string.IsNullOrEmpty(wireData.SourcePlayfieldItem)
						     && _switchPlayer.SwitchExists(wireData.SourcePlayfieldItem): {
						_switchPlayer.RegisterWire(wireData);

						break;
					}

					case SwitchSource.InputSystem:
						if (!_keyWireAssignments.ContainsKey(wireData.SourceInputAction)) {
							_keyWireAssignments[wireData.SourceInputAction] = new List<WireDestConfig>();
						}
						_keyWireAssignments[wireData.SourceInputAction].Add(new WireDestConfig(wireData));
						break;

					case SwitchSource.Playfield:
						Logger.Warn($"Cannot find wire switch \"{wireData.Src}\" on playfield!");
						break;

					case SwitchSource.Device
						when !string.IsNullOrEmpty(wireData.SourceDevice)
						     && _switchPlayer.SwitchDeviceExists(wireData.SourceDevice): {
						var deviceSwitch = _switchPlayer.Switch(wireData.SourceDevice, wireData.SourceDeviceItem);
						if (deviceSwitch != null) {
							deviceSwitch.AddWireDest(new WireDestConfig(wireData));
							Logger.Info($"Wiring device switch \"{wireData.Src}\" to \"{wireData.Dst}\"");

						} else {
							Logger.Warn($"Unknown switch \"{wireData.Src}\" to wire to \"{wireData.Dst}\".");
						}
						break;
					}
					case SwitchSource.Device when string.IsNullOrEmpty(wireData.SourceDevice):
						Logger.Warn($"Switch device not set for switch \"{wireData.Src}\".");
						break;

					case SwitchSource.Device when !_switchPlayer.SwitchDeviceExists(wireData.SourceDevice):
						Logger.Warn($"Unknown switch device \"{wireData.SourceDevice}\" to wire to \"{wireData.Dst}\".");
						break;

					case SwitchSource.Constant:
						break;

					default:
						Logger.Warn($"Unknown wire switch source \"{wireData.Source}\".");
						break;
				}
			}

			if (_keyWireAssignments.Count > 0) {
				_inputManager.Enable(HandleKeyInput);
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
							switch (wireConfig.Destination) {
								case WireDestination.Playfield:
									Wire(wireConfig.PlayfieldItem)?.OnChange(change == InputActionChange.ActionStarted);
									break;

								case WireDestination.Device:
									if (_wireDevices.ContainsKey(wireConfig.Device)) {
										var device = _wireDevices[wireConfig.Device];
										var wire = device.Wire(wireConfig.DeviceItem);
										if (wire != null) {
											wire.OnChange(change == InputActionChange.ActionStarted);
										} else {
											Logger.Warn($"Unknown wire \"{wireConfig.DeviceItem}\" in wire device \"{wireConfig.Device}\".");
										}
									}
									break;
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

	public struct WireDestConfig
	{
		public readonly int Destination;
		public readonly string PlayfieldItem;
		public readonly string Device;
		public readonly string DeviceItem;
		public readonly int PulseDelay;
		public bool IsPulseSource;

		public WireDestConfig(MappingsWireData data)
		{
			Destination = data.Destination;
			PlayfieldItem = data.DestinationPlayfieldItem;
			Device = data.DestinationDevice;
			DeviceItem = data.DestinationDeviceItem;
			PulseDelay = data.PulseDelay;
			IsPulseSource = false;
		}

		public WireDestConfig WithPulse(bool isPulseSource)
		{
			IsPulseSource = isPulseSource;
			return this;
		}
	}
}
