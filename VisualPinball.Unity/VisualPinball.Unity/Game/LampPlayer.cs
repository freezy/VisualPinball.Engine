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
		private readonly Dictionary<string, List<ILampDeviceComponent>> _lampAssignments = new();

		/// <summary>
		/// Links the GLE's IDs to the mappings.
		/// </summary>
		private readonly Dictionary<string, Dictionary<ILampDeviceComponent, LampMapping>> _lampMappings = new();

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
							HandleLampEvent(lampMapping.Id, LampStatus.Off);
						}
						else {
							HandleLampEvent(lampMapping.Id, LampStatus.On);
							HandleLampEvent(lampMapping.Id, 0f);
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
			LampAction action = default;
			foreach (var lampEvent in lampsEvent.LampsChanged) {
				if (Apply(lampEvent.Id, lampEvent.Source, lampEvent.IsCoil, ref action)) {
					ApplyValue(lampEvent.Id, lampEvent.Value, action.State, action.Lamp, action.Mapping);
				}
			}
		}

		private void HandleLampEvent(object sender, LampEventArgs lampEvent)
		{
			LampAction action = default;
			if (Apply(lampEvent.Id, lampEvent.Source, lampEvent.IsCoil, ref action)) {
				ApplyValue(lampEvent.Id, lampEvent.Value, action.State, action.Lamp, action.Mapping);
			}
		}

		public void HandleLampEvent(string id, float value)
		{
			LampAction action = default;
			if (Apply(id, LampSource.Lamp, false, ref action)) {
				ApplyValue(id, value, action.State, action.Lamp, action.Mapping);
			}
		}

		public void HandleLampEvent(string id, LampStatus status)
		{
			LampAction action = default;
			if (Apply(id, LampSource.Lamp, false, ref action)) {
				ApplyStatus(id, status, action.State, action.Lamp);
			}
		}

		public void HandleLampEvent(string id, Color color)
		{
			LampAction action = default;
			if (Apply(id, LampSource.Lamp, false, ref action)) {
				ApplyColor(id, color, action.State, action.Lamp);
			}
		}

		public void HandleCoilEvent(string id, bool isEnabled)
		{
			LampAction action = default;
			if (Apply(id, LampSource.Lamp, true, ref action)) {
				ApplyStatus(id, isEnabled ? LampStatus.On : LampStatus.Off, action.State, action.Lamp);
			}
		}

		private bool Apply(string id, LampSource lampSource, bool isCoil, ref LampAction action)
		{
			var hasChanged = false;
			if (_lampAssignments.ContainsKey(id)) {
				foreach (var component in _lampAssignments[id]) {
					var mapping = _lampMappings[id][component];
					if (mapping.Source != lampSource || mapping.IsCoil != isCoil) {
						// so, if we have a coil here that happens to have the same name as a lamp,
						// or a GI light with the same name as an other lamp, skip.
						continue;
					}
					if (_lamps.ContainsKey(component)) {
						var lamp = _lamps[component];
						var state = LampStates[id];
						action = new LampAction(state, lamp, mapping);
						hasChanged = true;
					}
				}

#if UNITY_EDITOR
				RefreshUI();
#endif
			} else {
				LampStates.TryAdd(id, LampState.Default);
				action = new LampAction(LampStates[id]);
				hasChanged = true;
			}

			return hasChanged;
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
			if (!_lampAssignments.ContainsKey(id)) {
				_lampAssignments[id] = new List<ILampDeviceComponent>();
			}
			if (!_lampMappings.ContainsKey(id)) {
				_lampMappings[id] = new Dictionary<ILampDeviceComponent, LampMapping>();
			}
			_lampAssignments[id].Add(lampMapping.Device);
			_lampMappings[id][lampMapping.Device] = lampMapping;
			LampStates[id] = new LampState(lampMapping.Device.LampStatus, lampMapping.Device.LampColor.ToEngineColor());
		}

		private readonly struct LampAction
		{
			public readonly LampState State;
			public readonly IApiLamp? Lamp;
			public readonly LampMapping? Mapping;

			public LampAction(LampState state)
			{
				State = state;
				Lamp = null;
				Mapping = null;
			}
			public LampAction(LampState state, IApiLamp lamp, LampMapping mapping)
			{
				State = state;
				Lamp = lamp;
				Mapping = mapping;
			}
		}

#if UNITY_EDITOR

		private UnityEditor.EditorWindow[] _lampManagerWindows;
		private bool _lampManagerWindowsInitialized;

		private void RefreshUI()
		{
			if (!_player!.UpdateDuringGamplay) {
				return;
			}

			if (!_lampManagerWindowsInitialized) {
				_lampManagerWindows = (UnityEditor.EditorWindow[])Resources.FindObjectsOfTypeAll(Type.GetType("VisualPinball.Unity.Editor.LampManager, VisualPinball.Unity.Editor"));
				_lampManagerWindowsInitialized = true;
			}

			foreach (var manager in _lampManagerWindows) {
				manager.Repaint();
			}
		}
#endif
	}
}
