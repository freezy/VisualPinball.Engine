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

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class CoilPlayer
	{
		/// <summary>
		/// Maps the coil component to the API class.
		/// </summary>
		private readonly Dictionary<ICoilDeviceComponent, IApiCoilDevice> _coilDevices = new();

		/// <summary>
		/// Maps the coil configuration ID to a destination.
		/// </summary>
		private readonly Dictionary<string, List<CoilDestConfig>> _coilAssignments = new();

		private Player? _player;
		private TableComponent? _tableComponent;
		private IGamelogicEngine? _gamelogicEngine;
		private LampPlayer? _lampPlayer;
		private WirePlayer? _wirePlayer;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		internal Dictionary<string, bool> CoilStatuses { get; } = new();
		internal void RegisterCoilDevice(ICoilDeviceComponent component, IApiCoilDevice coilDeviceApi) => _coilDevices[component] = coilDeviceApi;

		internal IApiCoil? Coil(ICoilDeviceComponent component, string coilItem) => _coilDevices.ContainsKey(component) ? _coilDevices[component].Coil(coilItem) : null;

		public void Awake(Player player, TableComponent tableComponent, IGamelogicEngine gamelogicEngine, LampPlayer lampPlayer, WirePlayer wirePlayer)
		{
			_player = player;
			_tableComponent = tableComponent;
			_gamelogicEngine = gamelogicEngine;
			_lampPlayer = lampPlayer;
			_wirePlayer = wirePlayer;
		}

		public void OnStart()
		{
			if (_gamelogicEngine != null && _tableComponent != null) {
				var config = _tableComponent.MappingConfig;
				_coilAssignments.Clear();
				foreach (var coilMapping in config.Coils) {
					switch (coilMapping.Destination) {
						case CoilDestination.Playfield:

							// mapping values must be set
							if (coilMapping.Device == null || string.IsNullOrEmpty(coilMapping.DeviceItem)) {
								Logger.Warn($"Ignoring unassigned device coil {coilMapping}");
								break;
							}

							// check if device exists
							if (!_coilDevices.ContainsKey(coilMapping.Device)) {
								Logger.Warn($"Unknown coil device \"{coilMapping.Device.name}\".");
								break;
							}

							var device = _coilDevices[coilMapping.Device];
							var coil = device.Coil(coilMapping.DeviceItem);
							if (coil != null) {
								AssignCoilMapping(coilMapping, false);

							} else {
								Logger.Error($"Unknown coil \"{coilMapping.DeviceItem}\" in coil device \"{coilMapping.Device}\".");
							}
							break;

						case CoilDestination.Lamp: {
							AssignCoilMapping(coilMapping, true);
							break;
						}

						default:
							throw new ArgumentOutOfRangeException();
					}
				}

				if (_coilAssignments.Count > 0) {
					_gamelogicEngine.OnCoilChanged += HandleCoilEvent;
				}
			}
		}

		/// <summary>
		/// Assigns a coil mapping with the coil's ID, but also with an int-parsed ID,
		/// so we can name them "01" and it still works with PinMAME.
		/// </summary>
		/// <param name="coilMapping">Mapping to assign</param>
		/// <param name="isLampCoil">If it's a flasher</param>
		private void AssignCoilMapping(CoilMapping coilMapping, bool isLampCoil)
		{
			AssignCoilMapping(coilMapping.Id, coilMapping, isLampCoil);
			if (int.TryParse(coilMapping.Id, out var id) && id.ToString() != coilMapping.Id) {
				AssignCoilMapping(id.ToString(), coilMapping, isLampCoil);
			}
		}

		private void AssignCoilMapping(string id, CoilMapping coilMapping, bool isLampCoil)
		{
			if (!_coilAssignments.ContainsKey(id)) {
				_coilAssignments[id] = new List<CoilDestConfig>();
			}
			var hasDynamicWire = _tableComponent!.MappingConfig.Wires.FirstOrDefault(w =>
				w.DestinationDevice == coilMapping.Device &&
				w.DestinationDeviceItem == coilMapping.DeviceItem &&
				w.IsDynamic) != null;

			_coilAssignments[id].Add(new CoilDestConfig(coilMapping.Device, coilMapping.DeviceItem, isLampCoil, hasDynamicWire));
			CoilStatuses[id] = false;
		}

		private void HandleCoilEvent(object sender, CoilEventArgs coilEvent)
		{
			// always save state
			CoilStatuses[coilEvent.Id] = coilEvent.IsEnabled;

			// trigger coil if mapped
			if (_coilAssignments.ContainsKey(coilEvent.Id)) {
				foreach (var destConfig in _coilAssignments[coilEvent.Id]) {

					if (destConfig.HasDynamicWire) {
						// goes back through the wire mapping, which will decide whether it has already sent the event or not
						_wirePlayer!.HandleCoilEvent(coilEvent.Id, coilEvent.IsEnabled);
						continue;
					}

					if (destConfig.IsLampCoil) {
						_lampPlayer!.HandleCoilEvent(coilEvent.Id, coilEvent.IsEnabled);
						continue;
					}

					// device coil?
					if (destConfig.Device != null) {

						// check device
						if (!_coilDevices.ContainsKey(destConfig.Device)) {
							Logger.Error($"Cannot trigger coil on non-existing device \"{destConfig.Device}\" for {coilEvent.Id}.");
							continue;
						}

						// check coil in device
						var coil = _coilDevices[destConfig.Device].Coil(destConfig.DeviceItem);
						if (coil == null) {
							Logger.Error($"Cannot trigger non-existing coil \"{destConfig.DeviceItem}\" in coil device \"{destConfig.Device.name}\" for {coilEvent.Id}.");
							continue;
						}

						coil.OnCoil(coilEvent.IsEnabled);

					} else {
						Logger.Error($"Cannot trigger unknown coil item \"{destConfig}\" for {coilEvent.Id}.");
					}
				}

#if UNITY_EDITOR
				RefreshUI();
#endif

			} else {
				Logger.Info($"Ignoring unassigned coil \"{coilEvent.Id}\".");
			}
		}

		public void OnDestroy()
		{
			if (_coilAssignments.Count > 0 && _gamelogicEngine != null) {
				_gamelogicEngine.OnCoilChanged -= HandleCoilEvent;
			}
		}

#if UNITY_EDITOR
		private void RefreshUI()
		{
			if (!_player!.UpdateDuringGameplay) {
				return;
			}

			foreach (var manager in (UnityEditor.EditorWindow[])Resources.FindObjectsOfTypeAll(Type.GetType("VisualPinball.Unity.Editor.CoilManager, VisualPinball.Unity.Editor"))) {
				manager.Repaint();
			}
		}
#endif
	}

	internal class CoilDestConfig
	{
		public readonly ICoilDeviceComponent? Device;
		public readonly string? DeviceItem;
		public readonly bool IsLampCoil;
		public readonly bool HasDynamicWire;

		public CoilDestConfig(ICoilDeviceComponent device, string deviceItem, bool isLampCoil, bool hasDynamicWire)
		{
			Device = device;
			DeviceItem = deviceItem;
			IsLampCoil = isLampCoil;
			HasDynamicWire = hasDynamicWire;
		}

		public override string ToString()
		{
			return $"coil destination (device = {Device}, coild id = {DeviceItem}, lamp = {IsLampCoil}, wire = {HasDynamicWire})";
		}
	}
}
