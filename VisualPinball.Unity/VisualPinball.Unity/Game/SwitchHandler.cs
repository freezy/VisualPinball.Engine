using System;
using System.Collections.Generic;
using NLog;
using Unity.Entities;
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
		private IGamelogicEngine Engine => _player.GamelogicEngine;

		/// <summary>
		/// The list of switches that need to be triggered in the gamelogic engine.
		/// </summary>
		private List<SwitchConfig> _switches;

		/// <summary>
		/// The list of game items that are wired directly to this switch.
		/// </summary>
		private List<WireDestConfig> _wires;

		private readonly Dictionary<string, ItemSwitchStatus> _switchStatuses = new Dictionary<string, ItemSwitchStatus>();

		private static VisualPinballSimulationSystemGroup SimulationSystemGroup => World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public SwitchHandler(string name, Player player, bool isEnabled = false)
		{
			Name = name;
			_player = player;
			IsEnabled = isEnabled;
		}

		/// <summary>
		/// Set up this switch to send its status to the gamelogic engine with the given ID.
		/// </summary>
		/// <param name="switchConfig">Config containing gamelogic engine's switch ID and pulse settings</param>
		internal IApiSwitchStatus AddSwitchDest(SwitchConfig switchConfig)
		{
			if (_switches == null) {
				_switches = new List<SwitchConfig>();
			}
			var swStatus = new ItemSwitchStatus(switchConfig.IsNormallyClosed) { IsSwitchEnabled = IsEnabled };
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
					Engine.Switch(switchConfig.SwitchId, switchConfig.IsNormallyClosed ? !enabled : enabled);
					_switchStatuses[switchConfig.SwitchId].IsSwitchEnabled = enabled;

					// if it's pulse, schedule to re-open
					if (enabled && switchConfig.IsPulseSwitch) {
						SimulationSystemGroup.ScheduleAction(switchConfig.PulseDelay,
							() => {
								Engine.Switch(switchConfig.SwitchId, switchConfig.IsNormallyClosed);
								IsEnabled = false;
								_switchStatuses[switchConfig.SwitchId].IsSwitchEnabled = false;
#if UNITY_EDITOR
								UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
							});
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
			UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
		}

		internal void ScheduleSwitch(bool enabled, int delay, Action<bool> onSwitched)
		{
			// handle switch -> gamelogic engine
			if (Engine != null && _switches != null) {
				foreach (var switchConfig in _switches) {
					SimulationSystemGroup.ScheduleAction(delay,
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
						SimulationSystemGroup.ScheduleAction(delay, () => dest.OnChange(enabled));

					} else {
						Logger.Warn($"Cannot find wire device \"{wireConfig.Device}\".");
					}
				}
			}

			// handle own status
			SimulationSystemGroup.ScheduleAction(delay, () => {
				Debug.Log($"Setting scheduled switch {Name} to {enabled}.");
				IsEnabled = enabled;

#if UNITY_EDITOR
				UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
				onSwitched.Invoke(enabled);
			});
		}
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
