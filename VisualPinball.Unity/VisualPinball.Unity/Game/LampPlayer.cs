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
using NLog;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Mappings;
using VisualPinball.Engine.VPT.Table;
using Color = UnityEngine.Color;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class LampPlayer
	{
		private readonly Dictionary<string, IApiLamp> _lamps = new Dictionary<string, IApiLamp>();
		private readonly Dictionary<string, List<string>> _lampAssignments = new Dictionary<string, List<string>>();
		private readonly Dictionary<string, MappingsLampData> _lampMappings = new Dictionary<string, MappingsLampData>();

		private Table _table;
		private IGamelogicEngine _gamelogicEngine;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		internal void RegisterLamp(IItem item, IApiLamp lampApi) => _lamps[item.Name] = lampApi;

		public void Awake(Table table, IGamelogicEngine gamelogicEngine)
		{
			_table = table;
			_gamelogicEngine = gamelogicEngine;
		}

		public void OnStart()
		{
			if (_gamelogicEngine is IGamelogicEngineWithLamps gamelogicEngineWithLamps) {
				var config = _table.Mappings;
				_lampAssignments.Clear();
				_lampMappings.Clear();
				foreach (var lampData in config.Data.Lamps) {
					switch (lampData.Destination) {
						case LampDestination.Playfield:

							if (string.IsNullOrEmpty(lampData.PlayfieldItem)) {
								Logger.Warn($"Ignoring unassigned lamp \"{lampData.Id}\".");
								break;
							}

							AssignLampMapping(lampData.Id, lampData);
							if (!string.IsNullOrEmpty(lampData.Green)) {
								AssignLampMapping(lampData.Green, lampData);
							}
							if (!string.IsNullOrEmpty(lampData.Blue)) {
								AssignLampMapping(lampData.Blue, lampData);
							}
							break;
					}
				}

				if (_lampAssignments.Count > 0) {
					gamelogicEngineWithLamps.OnLampChanged += HandleLampEvent;
					gamelogicEngineWithLamps.OnLampsChanged += HandleLampsEvent;
					gamelogicEngineWithLamps.OnLampColorChanged += HandleLampColorEvent;
				}
			}
		}

		public void HandleLampEvent(LampEventArgs lampEvent)
		{
			HandleLampEvent(null, lampEvent);
		}

		private void AssignLampMapping(string id, MappingsLampData lampData)
		{
			if (!_lampAssignments.ContainsKey(id)) {
				_lampAssignments[id] = new List<string>();
			}
			_lampAssignments[id].Add(lampData.PlayfieldItem);
			_lampMappings[id] = lampData;
		}

		private void HandleLampColorEvent(object sender, LampColorEventArgs lampEvent)
		{
			if (_lampAssignments.ContainsKey(lampEvent.Id)) {
				var mapping = _lampMappings[lampEvent.Id];
				foreach (var itemName in _lampAssignments[lampEvent.Id]) {
					if (_lamps.ContainsKey(itemName)) {
						var lamp = _lamps[itemName];
						switch (mapping.Type) {
							case LampType.Rgb:
								lamp.OnLampColor(lampEvent.Color);
								break;

							default:
								Logger.Error($"Received an RGB event for lamp {itemName} but lamp mapping type is {mapping.Type} ({mapping.Id})");
								break;
						}

					} else {
						Logger.Error($"Cannot trigger unknown lamp {itemName}.");
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
				HandleLampEvent(lampEvent, (lamp, mapping, itemName) => {
					var color = colors.ContainsKey(mapping.Id) ? colors[mapping.Id] : lamp.Color;
					if (lampEvent.Id == mapping.Id) {
						color.r = lampEvent.Value / 255f;

					} else if (lampEvent.Id == mapping.Green) {
						color.g = lampEvent.Value / 255f;

					} else if (lampEvent.Id == mapping.Blue) {
						color.b = lampEvent.Value / 255f;

					} else {
						Logger.Error($"Cannot assign lamp {lampEvent.Id} to an RGB value of light {itemName}");
					}
					colors[mapping.Id] = color;
					lamps[mapping.Id] = lamp;
				});
			}

			foreach (var mappingId in colors.Keys) {
				lamps[mappingId].Color = colors[mappingId];
			}
		}

		private void HandleLampEvent(object sender, LampEventArgs lampEvent)
		{
			HandleLampEvent(lampEvent, (lamp, mapping, itemName) => {
				if (lampEvent.Id == mapping.Id) {
					lamp.OnLamp(lampEvent.Value / 255f, ColorChannel.Red);

				} else if (lampEvent.Id == mapping.Green) {
					lamp.OnLamp(lampEvent.Value / 255f, ColorChannel.Green);

				} else if (lampEvent.Id == mapping.Blue) {
					lamp.OnLamp(lampEvent.Value / 255f, ColorChannel.Blue);

				} else {
					Logger.Error($"Cannot assign lamp {lampEvent.Id} to an RGB value of light {itemName}");
				}
			});
		}

		private void HandleLampEvent(LampEventArgs lampEvent, Action<IApiLamp, MappingsLampData, string> handleRgb)
		{
			if (_lampAssignments.ContainsKey(lampEvent.Id)) {
				var mapping = _lampMappings[lampEvent.Id];
				foreach (var itemName in _lampAssignments[lampEvent.Id]) {
					if (mapping.Source != lampEvent.Source) {
						// so, if we have a coil here that happens to have the same name as a lamp,
						// skip if the source isn't the same.
						continue;
					}
					if (_lamps.ContainsKey(itemName)) {
						var lamp = _lamps[itemName];
						switch (mapping.Type) {
							case LampType.SingleOnOff:
								lamp.OnLamp(lampEvent.Value > 0 ? 1f : 0f, ColorChannel.Alpha);
								break;

							case LampType.Rgb:
							case LampType.SingleFading:
								lamp.OnLamp(lampEvent.Value / 255f, ColorChannel.Alpha);
								break;

							case LampType.RgbMulti:
								handleRgb(lamp, mapping, itemName);
								break;

							default:
								Logger.Error($"Unknown mapping type \"{mapping.Type}\" of lamp ID {lampEvent.Id} for light {itemName}.");
								break;
						}

					} else {
						Logger.Error($"Cannot trigger unknown lamp {itemName}.");
					}
				}

			} else {
				Logger.Error($"Should update unassigned lamp {lampEvent.Id}.");
			}
		}

		public void OnDestroy()
		{
			if (_lampAssignments.Count > 0 && _gamelogicEngine is IGamelogicEngineWithLamps gamelogicEngineWithLamps) {
				gamelogicEngineWithLamps.OnLampColorChanged -= HandleLampColorEvent;
				gamelogicEngineWithLamps.OnLampChanged -= HandleLampEvent;
				gamelogicEngineWithLamps.OnLampsChanged -= HandleLampsEvent;
			}
		}
	}
}
