// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
using UnityEngine.InputSystem;
using Logger = NLog.Logger;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisualPinball.Unity
{
	public class WirePlayer
	{
		/// <summary>
		/// Maps the wire destination component to the wire destination API.
		/// </summary>
		private readonly Dictionary<IWireableComponent, IApiWireDeviceDest> _wireDevices = new Dictionary<IWireableComponent, IApiWireDeviceDest>();
		private readonly Dictionary<string, List<WireDestConfig>> _keyWireAssignments = new Dictionary<string, List<WireDestConfig>>();
		private readonly Dictionary<string, List<WireDestConfig>> _gleDestAssignments = new Dictionary<string, List<WireDestConfig>>();
		private readonly Dictionary<string, List<WireDestConfig>> _gleSrcAssignments = new Dictionary<string, List<WireDestConfig>>();
		private readonly Dictionary<WireDestConfig, Dictionary<bool, Queue<float>>> _gleSignals = new Dictionary<WireDestConfig, Dictionary<bool, Queue<float>>>();

		private Player _player;
		private PhysicsEngine _physicsEngine;
		private TableComponent _tableComponent;
		private InputManager _inputManager;
		private SwitchPlayer _switchPlayer;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		internal Dictionary<string, (bool, float)> WireStatuses { get; } = new Dictionary<string, (bool, float)>();

		internal IApiWireDeviceDest WireDevice(IWireableComponent c) => c != null &&  _wireDevices.ContainsKey(c) ? _wireDevices[c] : null;
		internal void RegisterWireDevice(IWireableComponent component, IApiWireDeviceDest wireDeviceApi) => _wireDevices[component] = wireDeviceApi;

		#region Lifecycle

		public void Awake(TableComponent tableComponent, InputManager inputManager, SwitchPlayer switchPlayer, Player player, PhysicsEngine physicsEngine)
		{
			_tableComponent = tableComponent;
			_inputManager = inputManager;
			_switchPlayer = switchPlayer;
			_player = player;
			_physicsEngine = physicsEngine;
		}

		public void OnStart()
		{
			_player.OnUpdate += OnUpdate;
			_keyWireAssignments.Clear();
			_gleDestAssignments.Clear();
			var config = _tableComponent.MappingConfig;
			foreach (var wireData in config.Wires) {
				AddWire(wireData);
			}

			_inputManager.Enable(HandleKeyInput);
		}

		private void OnUpdate(object sender, EventArgs e)
		{
			foreach (var wireConfig in _gleSignals.Keys) {
				if (!wireConfig.IsActive) {
					continue;
				}
				foreach (var isEnabled in _gleSignals[wireConfig].Keys) {
					var queue = _gleSignals[wireConfig][isEnabled];
					if (queue.Count == 0) {
						continue;
					}
					var lagSec = Time.realtimeSinceStartup - queue.Peek();
					if (lagSec > WireDestConfig.DynamicThresholdSec) {
						Logger.Info($"Disabling dynamic wire to {wireConfig.DeviceItem} @ {wireConfig.Device.gameObject.name} due to inactivity.");
						_wireDevices[wireConfig.Device].Wire(wireConfig.DeviceItem).OnChange(!isEnabled);
						wireConfig.IsActive = false;
					}
				}
			}
		}

		public void OnDestroy()
		{
			if (_keyWireAssignments.Count > 0) {
				_inputManager.Disable(HandleKeyInput);
			}
			_player.OnUpdate -= OnUpdate;
		}

		#endregion

		#region Setup

		internal void AddWire(WireMapping wireMapping, bool isHardwareRule = false)
		{
			WireStatuses[wireMapping.Id] = (false, 0);
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
					// todo
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

				_gleSignals[wireDest] = new Dictionary<bool, Queue<float>> {
					[true] = new Queue<float>(),
					[false] = new Queue<float>()
				};

				// reference the queue both by src and dest
				if (!_gleSrcAssignments.ContainsKey(srcId)) {
					_gleSrcAssignments[srcId] = new List<WireDestConfig>();
				}
				_gleSrcAssignments[srcId].Add(wireDest);

				if (!_gleDestAssignments.ContainsKey(destId)) {
					_gleDestAssignments[destId] = new List<WireDestConfig>();
				}
				_gleDestAssignments[destId].Add(wireDest);

				Logger.Info($"Added dynamic wire \"{wireMapping.Description}\" ({srcId} -> {destId}).");

			} else {
				Logger.Warn($"GLE IDs not found for dynamic wire {wireMapping.Description} ({srcId} -> {destId}).");
			}
			return wireDest;
		}

		private bool GetGamelogicEngineIds(WireMapping wireMapping, out string src, out string dest)
		{
			var sourceMapping = _tableComponent.MappingConfig.Switches.FirstOrDefault(switchMapping => {
				return switchMapping.Source switch {
					SwitchSource.InputSystem => switchMapping.InputActionMap == wireMapping.SourceInputActionMap && switchMapping.InputAction == wireMapping.SourceInputAction,
					SwitchSource.Playfield => switchMapping.Device == wireMapping.SourceDevice && switchMapping.DeviceItem == wireMapping.SourceDeviceItem,
					_ => false
				};
			});
			var destMapping = _tableComponent.MappingConfig.Coils.FirstOrDefault(coilMapping =>
				coilMapping.Device == wireMapping.DestinationDevice &&
				coilMapping.DeviceItem == wireMapping.DestinationDeviceItem);

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

		#endregion

		#region Runtime

		private void HandleKeyInput(object obj, InputActionChange change)
		{
			switch (change) {
				case InputActionChange.ActionStarted:
				case InputActionChange.ActionCanceled:
					var action = (InputAction)obj;
					if (_keyWireAssignments != null && _keyWireAssignments.ContainsKey(action.name)) {
						foreach (var wireConfig in _keyWireAssignments[action.name]) {
							if (wireConfig.Device == null || !_wireDevices.ContainsKey(wireConfig.Device)) {
								continue;
							}

							var device = _wireDevices[wireConfig.Device];
							var wire = device.Wire(wireConfig.DeviceItem);
							if (wire != null) {
								var isEnabled = change == InputActionChange.ActionStarted;
								if (!wireConfig.IsDynamic) {
									wire.OnChange(isEnabled);
									WireStatuses[wireConfig.Id] = (isEnabled, 0);

								} else {
									_gleSignals[wireConfig][isEnabled].Enqueue(Time.realtimeSinceStartup);
									if (wireConfig.IsActive) {
										// the dynamic wire is active, so trigger directly.
										wire.OnChange(isEnabled);
										WireStatuses[wireConfig.Id] = (isEnabled, -2);

									} else {
										WireStatuses[wireConfig.Id] = (WireStatuses[wireConfig.Id].Item1, -1);
									}
								}
#if UNITY_EDITOR
								RefreshUI();
#endif
							}
							else {
								Logger.Warn($"Unknown wire \"{wireConfig.DeviceItem}\" in wire device \"{wireConfig.Device}\".");
							}
						}
					}
					break;
			}
		}

		public void HandleSwitchChange(WireDestConfig wireConfig, bool isEnabled)
		{
			var device = _player.WireDevice(wireConfig.Device);
			if (device != null) {
				var wire = device.Wire(wireConfig.DeviceItem);
				if (wire != null) {

					if (!wireConfig.IsDynamic) {
						wire.OnChange(isEnabled);
						WireStatuses[wireConfig.Id] = (isEnabled, 0);

						// if it's pulse, schedule to re-open
						if (isEnabled && wireConfig.IsPulseSource) {
							_physicsEngine.ScheduleAction(wireConfig.PulseDelay, () => {
								wire.OnChange(false);
								WireStatuses[wireConfig.Id] = (false, 0);
#if UNITY_EDITOR
								RefreshUI();
#endif
							});
						}

					} else {
						_gleSignals[wireConfig][isEnabled].Enqueue(Time.realtimeSinceStartup);
						if (wireConfig.IsActive) {
							// the dynamic wire is active, so trigger directly.
							wire.OnChange(isEnabled);
							WireStatuses[wireConfig.Id] = (isEnabled, -2);

							// if it's pulse, schedule to re-open
							if (isEnabled && wireConfig.IsPulseSource) {
								_physicsEngine.ScheduleAction(wireConfig.PulseDelay, () => {
									wire.OnChange(false);
									WireStatuses[wireConfig.Id] = (false, -2);
#if UNITY_EDITOR
									RefreshUI();
#endif
								});
							}

						} else {
							WireStatuses[wireConfig.Id] = (WireStatuses[wireConfig.Id].Item1, -1);
						}
					}

#if UNITY_EDITOR
					RefreshUI();
#endif
				}

			} else {
				Logger.Warn($"Cannot find wire device \"{wireConfig.Device}\".");
			}
		}

		/// <summary>
		/// Gets called by the GLE for coils linked to a dynamic wire.
		/// </summary>
		/// <param name="id">GLE ID of the coil</param>
		/// <param name="isEnabled">Whether to enable or disable the coil.</param>
		public void HandleCoilEvent(string id, bool isEnabled)
		{
			if (!_gleDestAssignments.ContainsKey(id)) {
				return;
			}
			foreach (var wireConfig in _gleDestAssignments[id]) {
				if (wireConfig.Device == null || !_wireDevices.ContainsKey(wireConfig.Device)) {
					continue;
				}
				var now = Time.realtimeSinceStartup;
				var device = _wireDevices[wireConfig.Device];
				var wire = device.Wire(wireConfig.DeviceItem);
				if (wire != null) {
					var lagSec = GetLag(wireConfig, isEnabled);
					if (lagSec > 0) {

						// switch event was less than threshold ago, so let's check if the wire is active
						if (!wireConfig.IsActive) {
							// it wasn't active. so let's activate it
							Logger.Info($"Enabling dynamic wire from {id} to {wireConfig.DeviceItem} @ {wireConfig.Device.gameObject.name} ({lagSec}ms).");
							wireConfig.IsActive = true;
							wireConfig.ActiveSince = now;

							// since it wasn't active before, we need to emit, because the wire didn't catch it.
							wire.OnChange(isEnabled);
							WireStatuses[wireConfig.Id] = (isEnabled, lagSec);

						} else {
							// just update lag time
							WireStatuses[wireConfig.Id] = (WireStatuses[wireConfig.Id].Item1, lagSec);
						}

						// if it was already active, do nothing, since it was emitted by the wire already,
						// but that's only the case if it was *after* it became active. otherwise, emit.
						if (now - lagSec < wireConfig.ActiveSince) {
							wire.OnChange(isEnabled);
							WireStatuses[wireConfig.Id] = (isEnabled, lagSec);
						}

#if UNITY_EDITOR
						RefreshUI();
#endif

					} else {
						// so we got a coil with no or too old switch in the queue. we assume this comes from the GLE
						// without a corresponding switch event.
						wire.OnChange(isEnabled);
					}

				} else {
					Logger.Warn($"Unknown dynamic wire \"{wireConfig.DeviceItem}\" in wire device \"{wireConfig.Device}\".");
				}
			}
		}

		private float GetLag(WireDestConfig wireConfig, bool isEnabled)
		{
			var timeNow = Time.realtimeSinceStartup;
			var queue = _gleSignals[wireConfig][isEnabled];
			var lagSec = -1f;
			var numDequeue = 0;
			while (queue.Count > 0) {
				numDequeue++;
				var timeQueue = queue.Dequeue();
				lagSec = timeNow - timeQueue;
				if (lagSec < WireDestConfig.DynamicThresholdSec) {
					return lagSec;
				}
				lagSec = -1f - numDequeue;
			}
			return lagSec;
		}

		#endregion

#if UNITY_EDITOR
		private void RefreshUI()
		{
			if (!_player.UpdateDuringGamplay) {
				return;
			}

			foreach (var manager in (EditorWindow[])Resources.FindObjectsOfTypeAll(Type.GetType("VisualPinball.Unity.Editor.WireManager, VisualPinball.Unity.Editor")))
			{
				manager.Repaint();
			}
		}
#endif
	}

	public class WireDestConfig
	{
		/// <summary>
		/// Threshold in milliseconds within a confirming signal from the GLE
		/// is expected. Should the signal arrive outside the threshold, the
		/// dynamic wire's enabled status will be toggled.
		/// </summary>
		public const float DynamicThresholdSec = 600 / 1000f;

		/// <summary>
		/// ID reference from the wire, to easily identify the wire.
		/// </summary>
		public readonly string Id;

		/// <summary>
		/// Reference to the destination device.
		/// </summary>
		public readonly IWireableComponent Device;

		/// <summary>
		/// The item within the device.
		/// </summary>
		public readonly string DeviceItem;

		/// <summary>
		/// If true, this is a pulse source, in which case there is only the
		/// enable event and the disable event is automatically triggered
		/// after <see cref="PulseDelay"/> milliseconds.
		/// </summary>
		public bool IsPulseSource;

		/// <summary>
		/// If it's a pulse source, the pulse delay in milliseconds.
		/// </summary>
		public readonly int PulseDelay;

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
		/// (<see cref="DynamicThresholdSec"/>). If it's disabled, no signal
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
		public readonly bool IsDynamic;

		/// <summary>
		/// Status flag for dynamic wires. If true, the wire will emit events
		/// to the destination, otherwise it won't.
		/// </summary>
		internal bool IsActive;

		/// <summary>
		/// Status flag for dynamic wires. Timestamp (<see cref="UnityEngine.Time.realtimeSinceStartup"/>)
		/// when the wire became active.
		/// </summary>
		internal float ActiveSince;

		public WireDestConfig(WireMapping wireMapping)
		{
			Id = wireMapping.Id;
			Device = wireMapping.DestinationDevice;
			DeviceItem = wireMapping.DestinationDeviceItem;
			PulseDelay = wireMapping.PulseDelay;
			IsDynamic = wireMapping.IsDynamic;
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
