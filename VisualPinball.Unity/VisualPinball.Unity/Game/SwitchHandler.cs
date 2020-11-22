using System.Collections.Generic;
using NLog;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT;
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
		public bool IsClosed;

		private readonly string _name;
		private readonly Player _player;
		private readonly IGamelogicEngineWithSwitches _engine;

		/// <summary>
		/// The list of switches that need to be triggered in the gamelogic engine.
		/// </summary>
		private List<SwitchConfig> _switchIds;

		/// <summary>
		/// The list of game items that are wired directly to this switch.
		/// </summary>
		private List<WireDestConfig> _wires;

		private static VisualPinballSimulationSystemGroup SimulationSystemGroup => World.DefaultGameObjectInjectionWorld.GetOrCreateSystem<VisualPinballSimulationSystemGroup>();
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public SwitchHandler(string name, Player player, IGamelogicEngineWithSwitches engine)
		{
			_name = name;
			_player = player;
			_engine = engine;
		}

		/// <summary>
		/// Set up this switch to send its status to the gamelogic engine with the given ID.
		/// </summary>
		/// <param name="switchConfig">Config containing gamelogic engine's switch ID and pulse settings</param>
		public void AddSwitchId(SwitchConfig switchConfig)
		{
			if (_switchIds == null) {
				_switchIds = new List<SwitchConfig>();
			}
			_switchIds.Add(switchConfig);
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
		/// Sends the switch element to the gamelogic engine and linked wires.
		/// </summary>
		/// <param name="closed">Switch status</param>
		public void OnSwitch(bool closed)
		{
			// handle switch -> gamelogic engine
			if (_engine != null && _switchIds != null) {
				foreach (var switchConfig in _switchIds) {

					// set new status now
					_engine.Switch(switchConfig.SwitchId, closed);

					// if it's pulse, schedule to re-open
					if (closed && switchConfig.IsPulseSwitch) {
						SimulationSystemGroup.ScheduleSwitch(switchConfig.PulseDelay,
							() => _engine.Switch(switchConfig.SwitchId, false));
					}
				}
			}

			// handle switch -> wire
			if (_wires != null) {
				foreach (var wireConfig in _wires) {
					IApiWireDest dest = null;
					switch (wireConfig.Destination) {
						case WireDestination.Playfield:
							dest = _player.Wire(wireConfig.PlayfieldItem);
							break;

						case WireDestination.Device:
							var device = _player.WireDevice(wireConfig.Device);
							if (device != null) {
								device.Wire(wireConfig.DeviceItem)?.OnChange(closed);

							} else {
								Logger.Warn($"Cannot find wire device \"{wireConfig.Device}\".");
							}
							break;
					}

					// close the switch now
					dest?.OnChange(closed);

					// if it's pulse, schedule to re-open
					if (closed && wireConfig.IsPulseSource) {
						if (dest != null) {
							SimulationSystemGroup.ScheduleSwitch(wireConfig.PulseDelay,
								() => dest.OnChange(false));
						}
					}
				}
			}

			// handle own status
			IsClosed = closed;
		}

		public void ScheduleSwitch(bool closed, int delay)
		{
			// handle switch -> gamelogic engine
			if (_engine != null && _switchIds != null) {
				foreach (var switchConfig in _switchIds) {
					SimulationSystemGroup.ScheduleSwitch(delay,
						() => _engine.Switch(switchConfig.SwitchId, closed));
				}
			} else {
				Logger.Warn("Cannot schedule device switch.");
			}

			// handle switch -> wire
			if (_wires != null) {
				foreach (var wireConfig in _wires) {
					IApiWireDest dest = null;
					switch (wireConfig.Destination) {
						case WireDestination.Playfield:
							dest = _player.Wire(wireConfig.PlayfieldItem);
							break;

						case WireDestination.Device:
							var device = _player.WireDevice(wireConfig.Device);
							if (device != null) {
								dest = device.Wire(wireConfig.DeviceItem);

							} else {
								Logger.Warn($"Cannot find wire device \"{wireConfig.Device}\".");
							}
							break;
					}

					if (dest != null) {
						SimulationSystemGroup.ScheduleSwitch(delay,
							() => dest.OnChange(closed));
					}
				}
			}

			// handle own status
			SimulationSystemGroup.ScheduleSwitch(delay, () => {
				Debug.Log($"Setting scheduled switch {_name} to {closed}.");
				IsClosed = closed;
#if UNITY_EDITOR
				UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
			});
		}
	}
}
