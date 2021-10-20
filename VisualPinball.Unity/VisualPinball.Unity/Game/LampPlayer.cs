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

using System;
using System.Collections.Generic;
using System.Security.Authentication.ExtendedProtection;
using NLog;
using VisualPinball.Engine.Math;
using Color = UnityEngine.Color;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class LampPlayer
	{
		/// <summary>
		/// List of all registered lamp APIs.
		/// </summary>
		private readonly Dictionary<ILampDeviceComponent, IApiLamp> _lamps = new Dictionary<ILampDeviceComponent, IApiLamp>();

		/// <summary>
		/// Links the GLE's IDs to the lamps.
		/// </summary>
		private readonly Dictionary<string, List<ILampDeviceComponent>> _lampAssignments = new Dictionary<string, List<ILampDeviceComponent>>();

		/// <summary>
		/// Links the GLE's IDs to the mappings.
		/// </summary>
		private readonly Dictionary<string, Dictionary<ILampDeviceComponent, LampMapping>> _lampMappings = new Dictionary<string, Dictionary<ILampDeviceComponent, LampMapping>>();

		private TableComponent _tableComponent;
		private IGamelogicEngine _gamelogicEngine;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		internal IApiLamp Lamp(ILampDeviceComponent component)
			=> _lamps.ContainsKey(component) ? _lamps[component] : null;

		internal Dictionary<string, float> LampStatuses { get; } = new Dictionary<string, float>();
		internal void RegisterLamp(ILampDeviceComponent component, IApiLamp lampApi) => _lamps[component] = lampApi;

		public void Awake(TableComponent tableComponent, IGamelogicEngine gamelogicEngine)
		{
			_tableComponent = tableComponent;
			_gamelogicEngine = gamelogicEngine;
		}

		public void OnStart()
		{
			if (_gamelogicEngine != null) {
				var config = _tableComponent.MappingConfig;
				_lampAssignments.Clear();
				_lampMappings.Clear();
				foreach (var lampMapping in config.Lamps) {

					if (lampMapping.Device == null) {
						Logger.Warn($"Ignoring unassigned lamp \"{lampMapping.Id}\".");
						continue;
					}

					AssignLampMapping(lampMapping);

					// turn it off
					if (_lamps.ContainsKey(lampMapping.Device)) {
						_lamps[lampMapping.Device].OnLamp(0f, ColorChannel.Alpha);
					}
				}

				if (_lampAssignments.Count > 0) {
					_gamelogicEngine.OnLampChanged += HandleLampEvent;
					_gamelogicEngine.OnLampsChanged += HandleLampsEvent;
					_gamelogicEngine.OnLampColorChanged += HandleLampColorEvent;
				}
			}
		}

		public void HandleLampEvent(LampEventArgs lampEvent)
		{
			if (_lampAssignments.ContainsKey(lampEvent.Id)) {
				foreach (var component in _lampAssignments[lampEvent.Id]) {
					var mapping = _lampMappings[lampEvent.Id][component];
					if (mapping.Source != lampEvent.Source || mapping.IsCoil != lampEvent.IsCoil) {
						// so, if we have a coil here that happens to have the same name as a lamp,
						// or a GI light with the same name as an other lamp, skip.
						continue;
					}
					if (_lamps.ContainsKey(component)) {
						var lamp = _lamps[component];
						var value = 0f;
						var channel = ColorChannel.Alpha;
						switch (mapping.Type) {
							case LampType.SingleOnOff:
								value = lampEvent.Value > 0 ? 1f : 0f;
								break;

							case LampType.Rgb:
								value = lampEvent.Value / 255f; // todo test

								break;
							case LampType.RgbMulti:
								value = lampEvent.Value / 255f; // todo test
								channel = mapping.Channel;
								break;

							case LampType.SingleFading:
								value = lampEvent.Value / (float)mapping.FadingSteps;
								break;

							default:
								Logger.Error($"Unknown mapping type \"{mapping.Type}\" of lamp ID {lampEvent.Id} for light {component}.");
								break;
						}

						lamp.OnLamp(value, channel);
						LampStatuses[lampEvent.Id] = value;

					}
				}
#if UNITY_EDITOR
				UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
			}
		}

		public void OnDestroy()
		{
			if (_lampAssignments.Count > 0 && _gamelogicEngine != null) {
				_gamelogicEngine.OnLampColorChanged -= HandleLampColorEvent;
				_gamelogicEngine.OnLampChanged -= HandleLampEvent;
				_gamelogicEngine.OnLampsChanged -= HandleLampsEvent;
			}
		}


		private void AssignLampMapping(LampMapping lampMapping)
		{
			var id = lampMapping.Id;
			if (!_lampAssignments.ContainsKey(id)) {
				_lampAssignments[id] = new List<ILampDeviceComponent>();
			}
			if (!_lampMappings.ContainsKey(id)) {
				_lampMappings[id] = new Dictionary<ILampDeviceComponent, LampMapping>();
			}
			_lampAssignments[id].Add(lampMapping.Device);
			_lampMappings[id][lampMapping.Device] = lampMapping;
			LampStatuses[id] = 0f;
		}

		private void HandleLampColorEvent(object sender, LampColorEventArgs lampEvent)
		{
			if (_lampAssignments.ContainsKey(lampEvent.Id)) {
				foreach (var component in _lampAssignments[lampEvent.Id]) {
					var mapping = _lampMappings[lampEvent.Id][component];
					if (_lamps.ContainsKey(component)) {
						var lamp = _lamps[component];
						switch (mapping.Type) {
							case LampType.Rgb:
								lamp.OnLampColor(lampEvent.Color);
								break;

							default:
								Logger.Error($"Received an RGB event for lamp {component} but lamp mapping type is {mapping.Type} ({mapping.Id})");
								break;
						}

					} else {
						Logger.Error($"Cannot trigger unknown lamp {component}.");
					}
				}

			} else {
				Logger.Error($"Should update unassigned lamp {lampEvent.Id}.");
			}
		}

		private void HandleLampsEvent(object sender, LampsEventArgs lampsEvent)
		{
			var colors = new Dictionary<string, Color>();
			var lamps = new Dictionary<string, IApiLamp>();
			foreach (var lampEvent in lampsEvent.LampsChanged) {
				HandleLampEvent(lampEvent);
			}

			foreach (var mappingId in colors.Keys) {
				lamps[mappingId].Color = colors[mappingId];
				LampStatuses[mappingId] = colors[mappingId].grayscale;
			}
		}

		private void HandleLampEvent(object sender, LampEventArgs lampEvent)
		{
			HandleLampEvent(lampEvent);
		}
	}
}
