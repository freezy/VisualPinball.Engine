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
using UnityEngine.Networking.Types;

namespace VisualPinball.Unity
{
	public class WirePlayer
	{
		/// <summary>
		/// Maps the wire destination component to the wire destination API.
		/// </summary>
		private readonly Dictionary<IWireableComponent, IApiWireDeviceDest> _wireDevices = new Dictionary<IWireableComponent, IApiWireDeviceDest>();
		private readonly Dictionary<string, List<WireDestConfig>> _keyWireAssignments = new Dictionary<string, List<WireDestConfig>>();

		private TableComponent _tableComponent;
		private InputManager _inputManager;
		private SwitchPlayer _switchPlayer;

		private Dictionary<(string, string), Queue<float>> _dynamicWireSignals = new Dictionary<(string, string), Queue<float>>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		internal IApiWireDeviceDest WireDevice(IWireableComponent c) => _wireDevices.ContainsKey(c) ? _wireDevices[c] : null;
		internal void RegisterWireDevice(IWireableComponent component, IApiWireDeviceDest wireDeviceApi) => _wireDevices[component] = wireDeviceApi;

		public void Awake(TableComponent tableComponent, InputManager inputManager, SwitchPlayer switchPlayer)
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

		internal void AddWire(WireMapping wireMapping, bool isHardwareRule = false)
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
						deviceSwitch.AddWireDest(SetupWireDestConfig(wireMapping, isHardwareRule));
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
					_keyWireAssignments[wireMapping.SourceInputAction].Add(SetupWireDestConfig(wireMapping, isHardwareRule));
					break;
				}

				case SwitchSource.Constant:
					break;

				default:
					Logger.Warn($"Unknown wire switch source \"{wireMapping.Source}\".");
					break;
			}
		}

		private WireDestConfig SetupWireDestConfig(WireMapping wireMapping, bool isHardwareRule)
		{
			var wireDest = new WireDestConfig(wireMapping) { IsHardwareRule = isHardwareRule };
			if (!wireMapping.IsDynamic) {
				return wireDest;
			}
			if (GetGamelogicEngineIds(wireMapping, out var srcId, out var destId)) {
				wireMapping.DynamicSrcId = srcId;
				wireMapping.DynamicDestId = destId;
				_dynamicWireSignals[(srcId, destId)] = new Queue<float>();

			} else {
				Logger.Warn($"GLE IDs not found for dynamic wire {wireMapping.Description} ({srcId} / {destId}).");
			}
			return wireDest;
		}

		private bool GetGamelogicEngineIds(WireMapping wireMapping, out string src, out string dest)
		{
			var sourceMapping = _tableComponent.MappingConfig.Switches.FirstOrDefault(switchMapping =>
				switchMapping.Device == wireMapping.SourceDevice && switchMapping.DeviceItem == wireMapping.SourceDeviceItem);
			var destMapping = _tableComponent.MappingConfig.Coils.FirstOrDefault(coilMapping =>
				coilMapping.Device == wireMapping.DestinationDevice && coilMapping.DeviceItem == wireMapping.DestinationDeviceItem);
			src = sourceMapping?.Id;
			dest = destMapping?.Id;
			return sourceMapping != null && destMapping != null;
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
						.FirstOrDefault(a => a.IsHardwareRule && a.Device == wireMapping.DestinationDevice && a.DeviceItem == wireMapping.DestinationDeviceItem);
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
							if (!_wireDevices.ContainsKey(wireConfig.Device)) {
								continue;
							}

							var device = _wireDevices[wireConfig.Device];
							var wire = device.Wire(wireConfig.DeviceItem);
							if (wire != null) {
								var isEnabled = change == InputActionChange.ActionStarted;
								if (!wireConfig.IsDynamic) {
									wire.OnChange(isEnabled);

								} else {
									if (wireConfig.IsActive) {
										wire.OnChange(isEnabled);
									}
								}
							} else {
								Logger.Warn($"Unknown wire \"{wireConfig.DeviceItem}\" in wire device \"{wireConfig.Device}\".");
							}
						}
					}
					break;
			}
		}

		public void HandleCoilEvent()
		{

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
		public readonly IWireableComponent Device;
		public readonly string DeviceItem;
		public readonly int PulseDelay;
		public bool IsPulseSource;

		/// <summary>
		/// Wires that are added during gameplay (MPF does this). We tag
		/// it as such here so we can filter when removing the wire.
		/// </summary>
		public bool IsHardwareRule;

		/// <summary>
		/// Unlike hardware rules, dynamic wires are permanently added, but
		/// they dynamically enable and disable depending on the GLE. <p/>
		///
		/// If a dynamic wire is enabled, it stays active until the output
		/// signal isn't received from the GLE within a certain threshold
		/// (<see cref="DynamicThresholdMs"/>). If it's disabled, no signal
		/// is sent until we get it from the GLE.
		/// </summary>
		///
		/// <remarks>
		/// The goal is to compensate lag introduced by the GLE. The typical
		/// use case are flippers. By additionally linking the flipper button
		/// switch to the flipper coil with a dynamic wire, VPE will instantly
		/// trigger the flipper coil and only stop doing so if no coil signal
		/// is received from the GLE.
		/// </remarks>
		public bool IsDynamic;

		/// <summary>
		/// Threshold in milliseconds within a confirming signal from the GLE
		/// is expected. Should the signal arrive outside the threshold, the
		/// dynamic wire's enabled status will be toggled.
		/// </summary>
		public int DynamicThresholdMs;

		public bool IsActive = true;

		public WireDestConfig(WireMapping wireMapping)
		{
			Device = wireMapping.DestinationDevice;
			DeviceItem = wireMapping.DestinationDeviceItem;
			PulseDelay = wireMapping.PulseDelay;
			IsDynamic = wireMapping.IsDynamic;
			DynamicThresholdMs = wireMapping.DynamicThresholdMs;
			IsPulseSource = false;
			IsHardwareRule = false;
			IsActive = !wireMapping.IsDynamic;
		}

		public WireDestConfig WithPulse(bool isPulseSource)
		{
			IsPulseSource = isPulseSource;
			return this;
		}
	}
}
