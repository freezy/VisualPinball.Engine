﻿// Visual Pinball Engine
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
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Switches need a list of wires and links to the game engine.
	///
	/// Both switchable game items and device switches need this, so this class
	/// implements it for both.
	/// </summary>
	public class SwitchHandler
	{
		/// <summary>
		/// Whether the switch is enabled (or triggered, or "on"). Whether is the switch is
		/// "closed" - the value sent to the gamelogic engine - depends on each of the linked
		/// switch configuration's <see cref="SwitchConfig.IsNormallyClosed"/>.
		/// </summary>
		public bool IsEnabled;

		public readonly string Name;
		private readonly Player _player;
		private readonly PhysicsEngine _physicsEngine;

		private IGamelogicEngine Engine => _player.GamelogicEngine;

		/// <summary>
		/// The list of switches that need to be triggered in the gamelogic engine.
		/// </summary>
		private List<SwitchConfig> _switches;

		/// <summary>
		/// The list of game items that are wired directly to this switch.
		/// </summary>
		private List<WireDestConfig> _wires;

		private readonly Dictionary<string, IApiSwitchStatus> _switchStatuses = new Dictionary<string, IApiSwitchStatus>();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public SwitchHandler(string name, Player player, PhysicsEngine physicsEngine, bool isEnabled = false)
		{
			Name = name;
			_player = player;
			_physicsEngine = physicsEngine;
			IsEnabled = isEnabled;
		}

		/// <summary>
		/// Set up this switch to send its status to the gamelogic engine with the given ID.
		/// </summary>
		/// <param name="switchConfig">Config containing gamelogic engine's switch ID and pulse settings</param>
		/// <param name="switchStatus">Since multiple switch destinations can map to a switch, we might already have a status object.</param>
		internal IApiSwitchStatus AddSwitchDest(SwitchConfig switchConfig, IApiSwitchStatus switchStatus)
		{
			if (_switches == null) {
				_switches = new List<SwitchConfig>();
			}

			var swStatus = switchStatus ?? new ItemSwitchStatus(switchConfig.IsNormallyClosed) { IsSwitchEnabled = IsEnabled };
			_switches.Add(switchConfig);
			_switchStatuses[switchConfig.SwitchId] = swStatus;

			return swStatus;
		}

		/// <summary>
		/// Set up this switch to directly trigger another game item (coil or lamp), or
		/// a coil within a coil device.
		/// </summary>
		/// <param name="wireConfig">Configuration which game item to link to</param>
		public void AddWireDest(WireDestConfig wireConfig)
		{
			if (_wires == null) {
				_wires = new List<WireDestConfig>();
			}
			_wires.Add(wireConfig);
		}

		/// <summary>
		/// Removes a previously added wire.
		/// </summary>
		/// <param name="destId">Device ID of the destination</param>
		public void RemoveWireDest(string destId)
		{
			foreach (var wire in _wires) {
				if (wire.IsHardwareRule && wire.DeviceItem == destId) {
					_wires.Remove(wire);
					return;
				}
			}
		}

		public bool HasWireDest(IWireableComponent device, string deviceItem)
		{
			if (_wires == null)
				return false;
			foreach (var wire in _wires) {
				if (wire.Device == device && wire.DeviceItem == deviceItem)
					return true;
			}
			return false;
		}

		/// <summary>
		/// Sends the switch element to the gamelogic engine and linked wires.
		/// </summary>
		/// <param name="enabled">Switch status</param>
		internal void OnSwitch(bool enabled)
		{
			// handle switch -> gamelogic engine
			if (Engine != null && _switches != null) {
				foreach (var switchConfig in _switches) {

					// set new status now
					_switchStatuses[switchConfig.SwitchId].IsSwitchEnabled = enabled;
					Engine.Switch(switchConfig.SwitchId, switchConfig.IsNormallyClosed ? !enabled : enabled);

					// if it's pulse, schedule to re-open
					if (enabled && switchConfig.IsPulseSwitch) {
						_physicsEngine.ScheduleAction(
							switchConfig.PulseDelay,
							() => {
								_switchStatuses[switchConfig.SwitchId].IsSwitchEnabled = false;
								Engine.Switch(switchConfig.SwitchId, switchConfig.IsNormallyClosed);
								IsEnabled = false;
#if UNITY_EDITOR
								RefreshUI();
#endif
							}
						);
					}
				}
			}

			// handle switch -> wire
			if (_wires != null) {
				foreach (var wireConfig in _wires) {
					_player.HandleWireSwitchChange(wireConfig, enabled);
				}
			}

			// handle own status
			IsEnabled = enabled;

#if UNITY_EDITOR
			RefreshUI();
#endif
		}

		internal void ScheduleSwitch(bool enabled, int delay, Action<bool> onSwitched)
		{
			// handle switch -> gamelogic engine
			if (Engine != null && _switches != null) {
				foreach (var switchConfig in _switches) {
					_physicsEngine.ScheduleAction(delay,
						() => Engine.Switch(switchConfig.SwitchId, switchConfig.IsNormallyClosed ? !enabled : enabled));
				}
			} else {
				Logger.Warn("Cannot schedule device switch.");
			}

			// handle switch -> wire
			if (_wires != null) {
				foreach (var wireConfig in _wires) {
					var device = _player.WireDevice(wireConfig.Device);
					if (device != null) {
						var dest = device.Wire(wireConfig.DeviceItem);
						_physicsEngine.ScheduleAction(delay, () => dest.OnChange(enabled));

					} else {
						Logger.Warn($"Cannot find wire device \"{wireConfig.Device}\".");
					}
				}
			}

			// handle own status
			_physicsEngine.ScheduleAction(delay, () => {
				Debug.Log($"Setting scheduled switch {Name} to {enabled}.");
				IsEnabled = enabled;

				onSwitched.Invoke(enabled);

#if UNITY_EDITOR
				RefreshUI();
#endif
			});
		}

#if UNITY_EDITOR
		private void RefreshUI()
		{
			if (!_player.UpdateDuringGameplay) {
				return;
			}

			foreach (var manager in (UnityEditor.EditorWindow[])Resources.FindObjectsOfTypeAll(Type.GetType("VisualPinball.Unity.Editor.SwitchManager, VisualPinball.Unity.Editor"))) {
				manager.Repaint();
			}
		}
#endif
	}

	internal class ItemSwitchStatus : IApiSwitchStatus
	{
		private readonly bool _isNormallyClosed;

		public bool IsSwitchEnabled { get; set; }
		public bool IsSwitchClosed {
			get => _isNormallyClosed ? !IsSwitchEnabled : IsSwitchEnabled;
			set => IsSwitchEnabled = _isNormallyClosed ? !value : value;
		}

		public ItemSwitchStatus(bool normallyClosed)
		{
			_isNormallyClosed = normallyClosed;
		}
	}
}
