﻿// Visual Pinball Engine
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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	public class CoilPlayer
	{
		private readonly Dictionary<string, IApiCoil> _coils = new Dictionary<string, IApiCoil>();
		private readonly Dictionary<string, IApiCoilDevice> _coilDevices = new Dictionary<string, IApiCoilDevice>();
		private readonly Dictionary<string, List<CoilDestConfig>> _coilAssignments = new Dictionary<string, List<CoilDestConfig>>();

		private Table _table;
		private IGamelogicEngine _gamelogicEngine;
		private LampPlayer _lampPlayer;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		internal void RegisterCoil(IItem item, IApiCoil coilApi) => _coils[item.Name] = coilApi;
		internal void RegisterCoilDevice(IItem item, IApiCoilDevice coilDeviceApi) => _coilDevices[item.Name] = coilDeviceApi;

		public void Awake(Table table, IGamelogicEngine gamelogicEngine, LampPlayer lampPlayer)
		{
			_table = table;
			_gamelogicEngine = gamelogicEngine;
			_lampPlayer = lampPlayer;
		}

		public void OnStart()
		{
			if (_gamelogicEngine is IGamelogicEngineWithCoils gamelogicEngineWithCoils) {
				var config = _table.Mappings;
				_coilAssignments.Clear();
				foreach (var coilData in config.Data.Coils) {
					switch (coilData.Destination) {
						case CoilDestination.Playfield:

							if (string.IsNullOrEmpty(coilData.PlayfieldItem)) {
								Logger.Warn($"Ignoring unassigned coil \"{coilData.Id}\".");
								break;
							}

							AssignCoilMapping(coilData.Id, coilData.PlayfieldItem);
							if (coilData.Type == CoilType.DualWound) {
								AssignCoilMapping(coilData.HoldCoilId, coilData.PlayfieldItem, true);
							}
							break;

						case CoilDestination.Device:

							// mapping values must be set
							if (string.IsNullOrEmpty(coilData.Device) || string.IsNullOrEmpty(coilData.DeviceItem)) {
								Logger.Warn($"Ignoring unassigned device coil \"{coilData.Id}\".");
								break;
							}

							// check if device exists
							if (!_coilDevices.ContainsKey(coilData.Device)) {
								Logger.Error($"Unknown coil device \"{coilData.Device}\".");
								break;
							}

							var device = _coilDevices[coilData.Device];
							var coil = device.Coil(coilData.DeviceItem);
							if (coil != null) {
								AssignCoilMapping(coilData.Id, coilData.DeviceItem, deviceName: coilData.Device);

							} else {
								Logger.Error($"Unknown coil \"{coilData.DeviceItem}\" in coil device \"{coilData.Device}\".");
							}
							break;

						case CoilDestination.Lamp:
							AssignCoilMapping(coilData.Id, coilData.PlayfieldItem, isLampCoil: true);
							break;
					}
				}

				if (_coilAssignments.Count > 0) {
					gamelogicEngineWithCoils.OnCoilChanged += HandleCoilEvent;
				}
			}
		}

		private void AssignCoilMapping(string id, string playfieldItem, bool isHoldCoil = false, bool isLampCoil = false, string deviceName = null)
		{
			if (!_coilAssignments.ContainsKey(id)) {
				_coilAssignments[id] = new List<CoilDestConfig>();
			}
			_coilAssignments[id].Add(new CoilDestConfig(playfieldItem, isHoldCoil, isLampCoil, deviceName));
		}

		private void HandleCoilEvent(object sender, CoilEventArgs coilEvent)
		{
			if (_coilAssignments.ContainsKey(coilEvent.Id)) {
				foreach (var destConfig in _coilAssignments[coilEvent.Id]) {

					if (destConfig.IsLampCoil) {
						_lampPlayer.HandleLampEvent(new LampEventArgs(coilEvent.Id, coilEvent.IsEnabled ? 1 : 0, LampSource.Coils));
						continue;
					}

					// device coil?
					if (destConfig.DeviceName != null) {

						// check device
						if (!_coilDevices.ContainsKey(destConfig.DeviceName)) {
							Logger.Error($"Cannot trigger coil on non-existing device \"{destConfig.DeviceName}\" for {coilEvent.Id}.");
							continue;
						}

						// check coil in device
						var coil = _coilDevices[destConfig.DeviceName].Coil(destConfig.ItemName);
						if (coil == null) {
							Logger.Error($"Cannot trigger non-existing coil \"{destConfig.ItemName}\" in coil device \"{destConfig.DeviceName}\" for {coilEvent.Id}.");
							continue;
						}

						coil.OnCoil(coilEvent.IsEnabled, destConfig.IsHoldCoil);

					} else if (_coils.ContainsKey(destConfig.ItemName)) {
						_coils[destConfig.ItemName].OnCoil(coilEvent.IsEnabled, destConfig.IsHoldCoil);

					} else {
						Logger.Error($"Cannot trigger unknown coil item \"{destConfig.ItemName}\" for {coilEvent.Id}.");
					}
				}

			} else {
				Logger.Info($"Ignoring unassigned coil \"{coilEvent.Id}\".");
			}
		}

		public void OnDestroy()
		{
			if (_coilAssignments.Count > 0 && _gamelogicEngine is IGamelogicEngineWithCoils gamelogicEngineWithCoils) {
				gamelogicEngineWithCoils.OnCoilChanged -= HandleCoilEvent;
			}
		}
	}

	internal readonly struct CoilDestConfig
	{
		/// <summary>
		/// Playfield item if <see cref="DeviceName"/> is null or device item otherwise.
		/// </summary>
		public readonly string ItemName;
		public readonly bool IsHoldCoil;
		public readonly bool IsLampCoil;
		public readonly string DeviceName;

		public CoilDestConfig(string itemName, bool isHoldCoil, bool isLampCoil, string deviceName)
		{
			ItemName = itemName;
			IsHoldCoil = isHoldCoil;
			IsLampCoil = isLampCoil;
			DeviceName = deviceName;
		}
	}
}
