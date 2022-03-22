// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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
using NLog;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Color = VisualPinball.Engine.Math.Color;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	public class LampPlayer
	{
		/// <summary>
		/// List of all registered lamp APIs.
		/// </summary>
		private readonly Dictionary<ILampDeviceComponent, IApiLamp> _lamps = new();

		/// <summary>
		/// Links the GLE's IDs to the lamps.
		/// </summary>
		private readonly Dictionary<string, List<ILampDeviceComponent>> _lampAssignments = new();

		/// <summary>
		/// Links the GLE's IDs to the mappings.
		/// </summary>
		private readonly Dictionary<string, Dictionary<ILampDeviceComponent, Dictionary<int, LampMapping>>> _lampMappings = new();

		private Player? _player;
		private TableComponent? _tableComponent;
		private IGamelogicEngine? _gamelogicEngine;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		internal IApiLamp? Lamp(ILampDeviceComponent component) => _lamps.ContainsKey(component) ? _lamps[component] : null;

		internal Dictionary<string, LampState> LampStates { get; } = new();
		internal void RegisterLamp(ILampDeviceComponent component, IApiLamp lampApi) => _lamps[component] = lampApi;

		public void Awake(Player player, TableComponent tableComponent, IGamelogicEngine gamelogicEngine)
		{
			_player = player;
			_tableComponent = tableComponent;
			_gamelogicEngine = gamelogicEngine;
		}

		public void OnStart()
		{
			if (_gamelogicEngine != null) {
				var config = _tableComponent!.MappingConfig;
				_lampAssignments.Clear();
				_lampMappings.Clear();
				foreach (var lampMapping in config.Lamps) {

					if (lampMapping.Device == null) {
						Logger.Warn($"Ignoring unassigned lamp \"{lampMapping.Id}\".");
						continue;
					}

					AssignLampMapping(lampMapping);

					if (_lamps.ContainsKey(lampMapping.Device)) {
						// turn off non-rgb lamps, turn on rgb lamps, but set to channel to 0

						if (lampMapping.Type != LampType.RgbMulti) {
							HandleLampEvent(lampMapping.Id, lampMapping.InternalId, LampStatus.Off);
						}
						else {
							HandleLampEvent(lampMapping.Id, lampMapping.InternalId, LampStatus.On);
							HandleLampEvent(lampMapping.Id, lampMapping.InternalId, 0f);
						}
					}
				}

				if (_lampAssignments.Count > 0) {
					_gamelogicEngine.OnLampChanged += HandleLampEvent;
					_gamelogicEngine.OnLampsChanged += HandleLampsEvent;
				}
			}
		}

		private void HandleLampsEvent(object sender, LampsEventArgs lampsEvent)
		{
			foreach (var lampEvent in lampsEvent.LampsChanged) {
				Apply(lampEvent.Id, lampEvent.InternalId, lampEvent.Source, lampEvent.IsCoil, (state, lamp, mapping) => ApplyValue(lampEvent.Id, lampEvent.InternalId, lampEvent.Value, state, lamp, mapping));
			}
		}

		private void HandleLampEvent(object sender, LampEventArgs lampEvent)
		{
			Apply(lampEvent.Id, lampEvent.InternalId, lampEvent.Source, lampEvent.IsCoil, (state, lamp, mapping) => ApplyValue(lampEvent.Id, lampEvent.InternalId, lampEvent.Value, state, lamp, mapping));
		}

		public void HandleLampEvent(string id, int internalId, float value)
		{
			Apply(id, internalId, LampSource.Lamp, false, (state, lamp, mapping) => ApplyValue(id, internalId, value, state, lamp, mapping));
		}

		public void HandleLampEvent(string id, int internalId, LampStatus status)
		{
			Apply(id, internalId, LampSource.Lamp, false, (state, lamp, _) => ApplyStatus(id, status, state, lamp));
		}

		public void HandleLampEvent(string id, int internalId, Color color)
		{
			Apply(id, internalId, LampSource.Lamp, false, (state, lamp, _) => ApplyColor(id, color, state, lamp));
		}

		public void HandleCoilEvent(string id, int internalId, bool isEnabled)
		{
			Apply(id, internalId, LampSource.Lamp, true, (state, lamp, _) => ApplyStatus(id, isEnabled ? LampStatus.On : LampStatus.Off, state, lamp));
		}

		private void Apply(string id, int internalId, LampSource lampSource, bool isCoil, Action<LampState, IApiLamp?, LampMapping?> action)
		{
			if (_lampAssignments.ContainsKey(id)) {
				foreach (var component in _lampAssignments[id]) {

					if (!_lampMappings[id][component].ContainsKey(internalId)) {
						continue;
					}
					var mapping = _lampMappings[id][component][internalId];
					if (mapping.Source != lampSource || mapping.IsCoil != isCoil) {
						// so, if we have a coil here that happens to have the same name as a lamp,
						// or a GI light with the same name as an other lamp, skip.
						continue;
					}
					if (_lamps.ContainsKey(component)) {
						var lamp = _lamps[component];
						var state = LampStates[id];
						action(state, lamp, mapping);
					}
				}

				#if UNITY_EDITOR
				RefreshUI();
				#endif
			} else {
				if (!LampStates.ContainsKey(id)) {
					LampStates[id] = LampState.Default;
				}
				action(LampStates[id], null, null);
			}
		}

		private void ApplyStatus(string id, LampStatus status, LampState state, IApiLamp? lamp)
		{
			state.Status = status;
			LampStates[id] = state;
			lamp?.OnLamp(status);
		}

		private void ApplyColor(string id, Color color, LampState state, IApiLamp? lamp)
		{
			state.Color.SetColorWithoutAlpha(color);
			LampStates[id] = state;
			lamp?.OnLamp(state.Color.ToUnityColor());
		}

		private void ApplyValue(string id, int internalId, float value, LampState state, IApiLamp? lamp, LampMapping? mapping)
		{
			if (mapping == null) {
				// if not mapped, there is no lamp, so just save the state.
				// we do that by setting both status and intensity
				state.IsOn = value > 0;
				state.Color.Alpha = (int)value;
				LampStates[id] = state;
				return;
			}

			switch (mapping.Type) {
				case LampType.SingleOnOff:
					state.IsOn = value > 0;
					LampStates[id] = state;
					lamp?.OnLamp(state.Status);
					break;

				case LampType.Rgb:
					state.Color.Alpha = (int)value;
					LampStates[id] = state;
					lamp?.OnLamp(state.Intensity);
					break;

				case LampType.RgbMulti:
					state.SetChannel(mapping.Channel, value / 255f);
					LampStates[id] = state;
					lamp?.OnLamp(state.Color.ToUnityColor());
					break;

				case LampType.SingleFading:
					state.Intensity = value / mapping.FadingSteps;
					LampStates[id] = state;
					lamp?.OnLamp(state.Intensity);
					break;

				default:
					Logger.Error($"Unknown mapping type \"{mapping.Type}\" of lamp ID {id} for light {lamp}.");
					break;
			}
		}

		public void OnDestroy()
		{
			if (_lampAssignments.Count > 0 && _gamelogicEngine != null) {
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
				_lampMappings[id] = new Dictionary<ILampDeviceComponent, Dictionary<int, LampMapping>>();
			}
			_lampAssignments[id].Add(lampMapping.Device);
			if (!_lampMappings[id].ContainsKey(lampMapping.Device)) {
				_lampMappings[id][lampMapping.Device] = new Dictionary<int, LampMapping>();
			}
			_lampMappings[id][lampMapping.Device][lampMapping.InternalId] = lampMapping;

			if (!LampStates.ContainsKey(id)) {
				LampStates[id] = new LampState(lampMapping.Device.LampStatus, lampMapping.Device.LampColor.ToEngineColor());
			}
		}

#if UNITY_EDITOR
		private void RefreshUI()
		{
			if (!_player!.UpdateDuringGamplay) {
				return;
			}

			foreach (var manager in (UnityEditor.EditorWindow[])Resources.FindObjectsOfTypeAll(Type.GetType("VisualPinball.Unity.Editor.LampManager, VisualPinball.Unity.Editor"))) {
				manager.Repaint();
			}
		}
#endif
	}
}
