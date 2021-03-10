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
				AddWire(wireData);
			}

			_inputManager.Enable(HandleKeyInput);
		}

		internal void AddWire(MappingsWireData wireData, bool isDynamic = false)
		{
			switch (wireData.Source) {

				case SwitchSource.Playfield: {
					if (string.IsNullOrEmpty(wireData.SourcePlayfieldItem)) {
						break;
					}

					if (!_switchPlayer.SwitchExists(wireData.SourcePlayfieldItem)) {
						Logger.Error($"Cannot find item \"{wireData.SourcePlayfieldItem}\" for wire source.");
						break;
					}

					_switchPlayer.RegisterWire(wireData, isDynamic);
					break;
				}

				case SwitchSource.InputSystem: {
					if (!_keyWireAssignments.ContainsKey(wireData.SourceInputAction)) {
						_keyWireAssignments[wireData.SourceInputAction] = new List<WireDestConfig>();
					}
					_keyWireAssignments[wireData.SourceInputAction].Add(new WireDestConfig(wireData) { IsDynamic = isDynamic });
					break;
				}

				case SwitchSource.Device: {
					// mapping values must be set
					if (string.IsNullOrEmpty(wireData.SourceDevice) || string.IsNullOrEmpty(wireData.SourceDeviceItem)) {
						break;
					}

					// check if device exists
					if (!_switchPlayer.SwitchDeviceExists(wireData.SourceDevice)) {
						Logger.Error($"Unknown wire switch device \"{wireData.SourceDevice}\".");
						break;
					}

					var deviceSwitch = _switchPlayer.Switch(wireData.SourceDevice, wireData.SourceDeviceItem);
					if (deviceSwitch != null) {
						deviceSwitch.AddWireDest(new WireDestConfig(wireData) { IsDynamic = isDynamic });
						Logger.Info($"Wiring device switch \"{wireData.Src}\" to \"{wireData.Dst}\"");

					} else {
						Logger.Warn($"Unknown switch \"{wireData.Src}\" to wire to \"{wireData.Dst}\".");
					}
					break;
				}

				case SwitchSource.Constant:
					break;

				default:
					Logger.Warn($"Unknown wire switch source \"{wireData.Source}\".");
					break;
			}
		}

		internal void RemoveWire(MappingsWireData wireData)
		{
			switch (wireData.Source) {

				case SwitchSource.Playfield: {
					if (string.IsNullOrEmpty(wireData.SourcePlayfieldItem)) {
						break;
					}

					if (!_switchPlayer.SwitchExists(wireData.SourcePlayfieldItem)) {
						Logger.Error($"Cannot find item \"{wireData.SourcePlayfieldItem}\" for wire source.");
						break;
					}

					_switchPlayer.UnregisterWire(wireData);
					break;
				}

				case SwitchSource.InputSystem: {
					if (!_keyWireAssignments.ContainsKey(wireData.SourceInputAction)) {
						_keyWireAssignments[wireData.SourceInputAction] = new List<WireDestConfig>();
					}
					var assignment = _keyWireAssignments[wireData.SourceInputAction].FirstOrDefault(a => a.IsDynamic && a.DestinationId == wireData.DestinationId);
					_keyWireAssignments[wireData.SourceInputAction].Remove(assignment);
					break;
				}

				case SwitchSource.Device: {
					// mapping values must be set
					if (string.IsNullOrEmpty(wireData.SourceDevice) || string.IsNullOrEmpty(wireData.SourceDeviceItem)) {
						break;
					}

					// check if device exists
					if (!_switchPlayer.SwitchDeviceExists(wireData.SourceDevice)) {
						Logger.Error($"Unknown wire switch device \"{wireData.SourceDevice}\".");
						break;
					}

					var deviceSwitch = _switchPlayer.Switch(wireData.SourceDevice, wireData.SourceDeviceItem);
					if (deviceSwitch != null) {
						deviceSwitch.RemoveWireDest(wireData.DestinationId);

					} else {
						Logger.Warn($"Unknown switch \"{wireData.Src}\" to wire to \"{wireData.Dst}\".");
					}
					break;
				}

				case SwitchSource.Constant:
					break;

				default:
					Logger.Warn($"Unknown wire switch source \"{wireData.Source}\".");
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

		/// <summary>
		/// If the destination is dynamic, it means it was added during
		/// gameplay. MPF does this, and it's called a "hardware rule". We tag
		/// it as such here so we can filter when removing the wire.
		/// </summary>
		public bool IsDynamic;

		public string DestinationId => Destination == WireDestination.Device ? DeviceItem : PlayfieldItem;

		public WireDestConfig(MappingsWireData data)
		{
			Destination = data.Destination;
			PlayfieldItem = data.DestinationPlayfieldItem;
			Device = data.DestinationDevice;
			DeviceItem = data.DestinationDeviceItem;
			PulseDelay = data.PulseDelay;
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
