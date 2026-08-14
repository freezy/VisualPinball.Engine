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
		private readonly Dictionary<string, List<LampMapping>> _lampMappings = new();

		/// <summary>
		/// Combines separately addressed red, green and blue outputs per physical lamp.
		/// </summary>
		private readonly Dictionary<ILampDeviceComponent, LampState> _rgbStates = new();

		private Player? _player;
		private TableComponent? _tableComponent;
		private IGamelogicEngine? _gamelogicEngine;
		private int _loggedLampEvents;
		private int _loggedUnmappedLampEvents;
		private bool _logLampEvents;
		private readonly HashSet<string> _loggedUnmappedLampIds = new();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private const int MaxLoggedLampEvents = 24;
		private const int MaxLoggedUnmappedLampEvents = 24;

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
				_lampMappings.Clear();
				_rgbStates.Clear();
				var configuredMappings = 0;
				var assignedMappings = 0;
				var unassignedMappings = 0;
				foreach (var lampMapping in config.Lamps) {
					configuredMappings++;

					if (!IsAlive(lampMapping.Device)) {
						unassignedMappings++;
						continue;
					}

					AssignLampMapping(lampMapping);
					assignedMappings++;

					if (_lamps.ContainsKey(lampMapping.Device)) {
						// turn off non-rgb lamps, turn on rgb lamps, but set to channel to 0

						if (lampMapping.Type != LampType.RgbMulti) {
							HandleLampEvent(lampMapping.Id, LampStatus.Off, lampMapping.Source);
						}
						else {
							HandleLampEvent(lampMapping.Id, LampStatus.On, lampMapping.Source);
							HandleLampEvent(lampMapping.Id, 0f, lampMapping.Source);
						}
					}
				}
				Logger.Info(
					$"LampPlayer mapped lamps: configured={configuredMappings}, assigned={assignedMappings}, " +
					$"unassigned={unassignedMappings}, assignmentKeys={_lampMappings.Count}, registeredApis={_lamps.Count}.");
				_logLampEvents = true;

				if (_lampMappings.Count > 0) {
					_gamelogicEngine.OnLampChanged += HandleLampEvent;
					_gamelogicEngine.OnLampsChanged += HandleLampsEvent;
				}
			}
		}

		private void HandleLampsEvent(object sender, LampsEventArgs lampsEvent)
		{
			foreach (var lampEvent in lampsEvent.LampsChanged) {
				Apply(lampEvent.Id, lampEvent.Source, lampEvent.IsCoil, LampUpdate.ForValue(lampEvent.Value));
			}
		}

		private void HandleLampEvent(object sender, LampEventArgs lampEvent)
		{
			Apply(lampEvent.Id, lampEvent.Source, lampEvent.IsCoil, LampUpdate.ForValue(lampEvent.Value));
		}

		public void HandleLampEvent(string id, float value)
		{
			HandleLampEvent(id, value, LampSource.Lamp);
		}

		public void HandleLampEvent(string id, float value, LampSource source)
		{
			Apply(id, source, false, LampUpdate.ForValue(value));
		}

		public void HandleLampEvent(string id, LampStatus status)
		{
			HandleLampEvent(id, status, LampSource.Lamp);
		}

		public void HandleLampEvent(string id, LampStatus status, LampSource source)
		{
			Apply(id, source, false, LampUpdate.ForStatus(status));
		}

		public void HandleLampEvent(string id, Color color)
		{
			HandleLampEvent(id, color, LampSource.Lamp);
		}

		public void HandleLampEvent(string id, Color color, LampSource source)
		{
			Apply(id, source, false, LampUpdate.ForColor(color));
		}

		public void HandleCoilEvent(string id, bool isEnabled)
		{
			Apply(id, LampSource.Lamp, true, LampUpdate.ForStatus(isEnabled ? LampStatus.On : LampStatus.Off));
		}

		private bool Apply(string id, LampSource lampSource, bool isCoil, LampUpdate update)
		{
			var hasChanged = false;
			if (_lampMappings.TryGetValue(id, out var mappings)) {
				foreach (var mapping in mappings) {
					if (mapping.Source != lampSource || mapping.IsCoil != isCoil) {
						// so, if we have a coil here that happens to have the same name as a lamp,
						// or a GI light with the same name as an other lamp, skip.
						continue;
					}
					var component = mapping.Device;
					if (IsAlive(component) && _lamps.TryGetValue(component, out var lamp)) {
						var state = LampStates[id];
						ApplyUpdate(id, update, state, lamp, mapping);
						hasChanged = true;
					}
				}
				LogLampEvent(id, lampSource, isCoil, hasChanged, mapped: true);

#if UNITY_EDITOR
				RefreshUI();
#endif
			} else {
				LampStates.TryAdd(id, LampState.Default);
				ApplyUpdate(id, update, LampStates[id], null, null);
				hasChanged = true;
				LogLampEvent(id, lampSource, isCoil, true, mapped: false);
			}

			return hasChanged;
		}

		private void ApplyUpdate(string id, LampUpdate update, LampState state, IApiLamp? lamp, LampMapping? mapping)
		{
			switch (update.Kind) {
				case LampUpdateKind.Value:
					ApplyValue(id, update.Value, state, lamp, mapping);
					break;
				case LampUpdateKind.Status:
					ApplyStatus(id, update.Status, state, lamp);
					break;
				case LampUpdateKind.Color:
					ApplyColor(id, update.Color, state, lamp, mapping);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private void LogLampEvent(string id, LampSource source, bool isCoil, bool applied, bool mapped)
		{
			if (!_logLampEvents) {
				return;
			}

			if (_loggedLampEvents < MaxLoggedLampEvents) {
				_loggedLampEvents++;
				Logger.Info(
					$"LampPlayer event sample #{_loggedLampEvents}: id={id}, source={source}, " +
					$"isCoil={isCoil}, mapped={mapped}, applied={applied}.");
			}

			if (mapped || _loggedUnmappedLampEvents >= MaxLoggedUnmappedLampEvents || !_loggedUnmappedLampIds.Add($"{source}:{isCoil}:{id}")) {
				return;
			}

			_loggedUnmappedLampEvents++;
			Logger.Warn($"LampPlayer unmapped incoming lamp event: id={id}, source={source}, isCoil={isCoil}.");
		}

		private void ApplyStatus(string id, LampStatus status, LampState state, IApiLamp? lamp)
		{
			state.Status = status;
			LampStates[id] = state;
			lamp?.OnLamp(status);
		}

		private void ApplyColor(string id, Color color, LampState state, IApiLamp? lamp, LampMapping? mapping)
		{
			state.Color.SetColorWithoutAlpha(color);
			LampStates[id] = state;
			if (mapping?.Device != null && _rgbStates.TryGetValue(mapping.Device, out var rgbState)) {
				rgbState.Color.SetColorWithoutAlpha(color);
				_rgbStates[mapping.Device] = rgbState;
				lamp?.OnLamp(rgbState.Color.ToUnityColor());
			} else {
				lamp?.OnLamp(state.Color.ToUnityColor());
			}
		}

		private void ApplyValue(string id, float value, LampState state, IApiLamp? lamp, LampMapping? mapping)
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
					if (mapping.Device != null && lamp != null) {
						if (!_rgbStates.TryGetValue(mapping.Device, out var rgbState)) {
							rgbState = new LampState(state.Status, state.Color.Clone());
						}
						rgbState.SetChannel(mapping.Channel, value / 255f);
						_rgbStates[mapping.Device] = rgbState;
						lamp.OnLamp(rgbState.Color.ToUnityColor());
					}
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
			if (_lampMappings.Count > 0 && _gamelogicEngine != null) {
				_gamelogicEngine.OnLampChanged -= HandleLampEvent;
				_gamelogicEngine.OnLampsChanged -= HandleLampsEvent;
			}
		}

		/// <summary>
		/// Assigns a lamp mapping with the lamp's ID, but also with an int-parsed ID,
		/// so we can name them "01" and it still works with PinMAME.
		/// </summary>
		/// <param name="lampMapping"></param>
		private void AssignLampMapping(LampMapping lampMapping)
		{
			AssignLampMapping(lampMapping.Id, lampMapping);
			if (int.TryParse(lampMapping.Id, out var id) && id.ToString() != lampMapping.Id) {
				AssignLampMapping(id.ToString(), lampMapping);
			}
		}

		private void AssignLampMapping(string id, LampMapping lampMapping)
		{
			if (!_lampMappings.ContainsKey(id)) {
				_lampMappings[id] = new List<LampMapping>();
			}
			var existingIndex = _lampMappings[id].FindIndex(existing => existing.Device == lampMapping.Device
				&& existing.Source == lampMapping.Source && existing.IsCoil == lampMapping.IsCoil
				&& existing.Channel == lampMapping.Channel);
			if (existingIndex >= 0) _lampMappings[id][existingIndex] = lampMapping;
			else _lampMappings[id].Add(lampMapping);
			LampStates[id] = new LampState(lampMapping.Device.LampStatus, lampMapping.Device.LampColor.ToEngineColor());
		}

		private static bool IsAlive(ILampDeviceComponent device)
		{
			return device is MonoBehaviour behaviour ? behaviour : device != null;
		}

		private enum LampUpdateKind
		{
			Value,
			Status,
			Color,
		}

		private readonly struct LampUpdate
		{
			internal LampUpdateKind Kind { get; }
			internal float Value { get; }
			internal LampStatus Status { get; }
			internal Color Color { get; }

			private LampUpdate(LampUpdateKind kind, float value, LampStatus status, Color color)
			{
				Kind = kind;
				Value = value;
				Status = status;
				Color = color;
			}

			internal static LampUpdate ForValue(float value) => new(LampUpdateKind.Value, value, default, default);
			internal static LampUpdate ForStatus(LampStatus status) => new(LampUpdateKind.Status, default, status, default);
			internal static LampUpdate ForColor(Color color) => new(LampUpdateKind.Color, default, default, color);
		}

#if UNITY_EDITOR

		private UnityEditor.EditorWindow[]? _lampManagerWindows;
		private bool _lampManagerWindowsInitialized;

		private void RefreshUI()
		{
			if (!_player!.UpdateDuringGameplay) {
				return;
			}

			if (!_lampManagerWindowsInitialized) {
				_lampManagerWindows = (UnityEditor.EditorWindow[])Resources.FindObjectsOfTypeAll(Type.GetType("VisualPinball.Unity.Editor.LampManager, VisualPinball.Unity.Editor"));
				_lampManagerWindowsInitialized = true;
			}

			foreach (var manager in _lampManagerWindows!) {
				manager.Repaint();
			}
		}
#endif
	}
}
