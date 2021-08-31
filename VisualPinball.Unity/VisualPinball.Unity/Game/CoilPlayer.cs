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
using UnityEditorInternal;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{
	public class CoilPlayer
	{
		/// <summary>
		/// Maps the coil component to the API class.
		/// </summary>
		private readonly Dictionary<ICoilDeviceAuthoring, IApiCoilDevice> _coilDevices = new Dictionary<ICoilDeviceAuthoring, IApiCoilDevice>();

		/// <summary>
		/// Maps the coil configuration ID to a destination.
		/// </summary>
		private readonly Dictionary<string, List<CoilDestConfig>> _coilAssignments = new Dictionary<string, List<CoilDestConfig>>();

		private TableAuthoring _tableComponent;
		private IGamelogicEngine _gamelogicEngine;
		private LampPlayer _lampPlayer;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		internal Dictionary<string, bool> CoilStatuses { get; } = new Dictionary<string, bool>();
		internal void RegisterCoilDevice(ICoilDeviceAuthoring component, IApiCoilDevice coilDeviceApi) => _coilDevices[component] = coilDeviceApi;

		public void Awake(TableAuthoring tableComponent, IGamelogicEngine gamelogicEngine, LampPlayer lampPlayer)
		{
			_tableComponent = tableComponent;
			_gamelogicEngine = gamelogicEngine;
			_lampPlayer = lampPlayer;
		}

		public void OnStart()
		{
			if (_gamelogicEngine != null) {
				var config = _tableComponent.MappingConfig;
				_coilAssignments.Clear();
				foreach (var coilMapping in config.Coils) {
					switch (coilMapping.Destination) {
						case ECoilDestination.Playfield:

							// mapping values must be set
							if (coilMapping.Device == null || string.IsNullOrEmpty(coilMapping.DeviceCoilId)) {
								Logger.Warn($"Ignoring unassigned device coil {coilMapping}");
								break;
							}

							// check if device exists
							if (!_coilDevices.ContainsKey(coilMapping.Device)) {
								Logger.Error($"Unknown coil device \"{coilMapping.Device.name}\".");
								break;
							}

							var device = _coilDevices[coilMapping.Device];
							var coil = device.Coil(coilMapping.DeviceCoilId);
							if (coil != null) {
								AssignCoilMapping(coilMapping.Id, coilMapping.Device, coilMapping.DeviceCoilId);

							} else {
								Logger.Error($"Unknown coil \"{coilMapping.DeviceCoilId}\" in coil device \"{coilMapping.Device}\".");
							}
							break;

						case ECoilDestination.Lamp:
							AssignCoilMapping(coilMapping.Id, coilMapping.Device, coilMapping.DeviceCoilId, isLampCoil: true);
							break;
					}
				}

				if (_coilAssignments.Count > 0) {
					_gamelogicEngine.OnCoilChanged += HandleCoilEvent;
				}
			}
		}

		private void AssignCoilMapping(string id, ICoilDeviceAuthoring device, string deviceCoilId, bool isHoldCoil = false, bool isLampCoil = false)
		{
			if (!_coilAssignments.ContainsKey(id)) {
				_coilAssignments[id] = new List<CoilDestConfig>();
			}
			_coilAssignments[id].Add(new CoilDestConfig(device, deviceCoilId, isHoldCoil, isLampCoil));
			CoilStatuses[id] = false;
		}

		private void HandleCoilEvent(object sender, CoilEventArgs coilEvent)
		{
			if (_coilAssignments.ContainsKey(coilEvent.Id)) {
				CoilStatuses[coilEvent.Id] = coilEvent.IsEnabled;

				foreach (var destConfig in _coilAssignments[coilEvent.Id]) {

					if (destConfig.IsLampCoil) {
						_lampPlayer.HandleLampEvent(new LampEventArgs(coilEvent.Id, coilEvent.IsEnabled ? 1 : 0, LampSource.Coils));
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
						var coil = _coilDevices[destConfig.Device].Coil(destConfig.DeviceCoilId);
						if (coil == null) {
							Logger.Error($"Cannot trigger non-existing coil \"{destConfig.DeviceCoilId}\" in coil device \"{destConfig.Device.name}\" for {coilEvent.Id}.");
							continue;
						}

						coil.OnCoil(coilEvent.IsEnabled, destConfig.IsHoldCoil);

					} else {
						Logger.Error($"Cannot trigger unknown coil item \"{destConfig}\" for {coilEvent.Id}.");
					}
				}

#if UNITY_EDITOR
				InternalEditorUtility.RepaintAllViews();
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

		// todo remove below
		internal void RegisterCoil(string goName, IApiCoil _)
		{
		}

		internal void RegisterCoilDevice(string goName, IApiCoilDevice _)
		{
		}

	}

	internal class CoilDestConfig
	{
		public readonly ICoilDeviceAuthoring Device;
		public readonly string DeviceCoilId;
		public readonly bool IsHoldCoil;
		public readonly bool IsLampCoil;

		public CoilDestConfig(ICoilDeviceAuthoring device, string deviceCoilId, bool isHoldCoil, bool isLampCoil)
		{
			Device = device;
			DeviceCoilId = deviceCoilId;
			IsHoldCoil = isHoldCoil;
			IsLampCoil = isLampCoil;
		}

		public override string ToString()
		{
			return $"coil destination (device = {Device}, coild id = {DeviceCoilId}, hold/lamp = {IsHoldCoil}/{IsLampCoil})";
		}
	}
}
