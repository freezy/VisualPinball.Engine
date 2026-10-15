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
using ColorChannel = VisualPinball.Engine.Math.ColorChannel;
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
		/// Links the GLE's IDs to the mappings, every mapping of an ID driven in turn.
		/// </summary>
		private readonly Dictionary<string, List<LampMapping>> _lampAssignments = new();

		/// <summary>
		/// The API each mapping drives: the device's own, or the one of the device's
		/// lamps the mapping names, such as one bulb of an insert with several.
		/// </summary>
		private readonly Dictionary<LampMapping, IApiLamp> _mappedLamps = new();

		/// <summary>
		/// The RGB channels each lamp is made of. A multi-channel lamp is one light the
		/// GLE drives through several IDs, so the colour has to be composed from all of
		/// them - taking it from whichever channel wrote last would drop the other two.
		/// Keyed by the lamp the channels drive, so two bulbs of one insert keep their
		/// colours apart, and two names for the same lamp share one colour.
		/// </summary>
		private readonly Dictionary<IApiLamp, List<LampMapping>> _rgbMultiChannels = new();

		private Player? _player;
		private TableComponent? _tableComponent;
		private IGamelogicEngine? _gamelogicEngine;
		private int _loggedLampEvents;
		private int _loggedUnmappedLampEvents;
		private bool _logLampEvents;
		private readonly HashSet<string> _loggedUnmappedLampIds = new();

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private readonly LogOncePerKey<string> _unknownLampTypeLog = new();
		private const int MaxLoggedLampEvents = 24;
		private const int MaxLoggedUnmappedLampEvents = 24;

		internal IApiLamp? Lamp(ILampDeviceComponent component) => _lamps.ContainsKey(component) ? _lamps[component] : null;

		/// <summary>The API of one of a registered device's lamps, the device's own where it has one.</summary>
		internal IApiLamp? Lamp(ILampDeviceComponent component, string deviceItem)
			=> _lamps.TryGetValue(component, out var api) ? ItemLamp(api, deviceItem) : null;

		/// <summary>
		/// The lamp a mapping's device item names on the device's registered API: the
		/// API itself unless it is a device of several lamps, such as an insert with
		/// its bulbs, and the item is one of them.
		/// </summary>
		private static IApiLamp ItemLamp(IApiLamp api, string deviceItem)
			=> api is IApiLampDevice device ? device.Lamp(deviceItem) ?? api : api;

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
				_mappedLamps.Clear();
				_rgbMultiChannels.Clear();
				var configuredMappings = 0;
				var assignedMappings = 0;
				var unassignedMappings = 0;
				foreach (var lampMapping in config.Lamps) {
					configuredMappings++;

					if (lampMapping.Device == null) {
						unassignedMappings++;
						continue;
					}

					// A device registers itself in Awake. An ExecuteAlways device had
					// its Awake in edit mode already, and Enter Play Mode without a
					// scene reload does not call it again, so such a device never
					// arrives here on its own: take it now, and initialise it the way
					// the ones that did were before OnStart.
					if (!_lamps.ContainsKey(lampMapping.Device) && lampMapping.Device is MonoBehaviour behaviour && _player != null) {
						var api = lampMapping.Device.GetApi(_player);
						_player.Register(api, behaviour);
						api.OnInit(_player.BallManager);
					}

					AssignLampMapping(lampMapping);
					assignedMappings++;

					if (lampMapping.Type == LampType.RgbMulti && _mappedLamps.TryGetValue(lampMapping, out var mappedLamp)) {
						if (!_rgbMultiChannels.ContainsKey(mappedLamp)) {
							_rgbMultiChannels[mappedLamp] = new List<LampMapping>();
						}
						_rgbMultiChannels[mappedLamp].Add(lampMapping);
					}

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
					$"unassigned={unassignedMappings}, assignmentKeys={_lampAssignments.Count}, registeredApis={_lamps.Count}.");
				_logLampEvents = true;

				if (_lampAssignments.Count > 0) {
					_gamelogicEngine.OnLampChanged += HandleLampEvent;
					_gamelogicEngine.OnLampsChanged += HandleLampsEvent;
				}
			}
		}

		private void HandleLampsEvent(object sender, LampsEventArgs lampsEvent)
		{
			foreach (var lampEvent in lampsEvent.LampsChanged) {
				Dispatch(lampEvent.Id, lampEvent.Source, lampEvent.IsCoil, LampEvent.Of(lampEvent.Value));
			}
		}

		private void HandleLampEvent(object sender, LampEventArgs lampEvent)
		{
			Dispatch(lampEvent.Id, lampEvent.Source, lampEvent.IsCoil, LampEvent.Of(lampEvent.Value));
		}

		public void HandleLampEvent(string id, float value)
		{
			HandleLampEvent(id, value, LampSource.Lamp);
		}

		public void HandleLampEvent(string id, float value, LampSource source)
		{
			Dispatch(id, source, false, LampEvent.Of(value));
		}

		public void HandleLampEvent(string id, LampStatus status)
		{
			HandleLampEvent(id, status, LampSource.Lamp);
		}

		public void HandleLampEvent(string id, LampStatus status, LampSource source)
		{
			Dispatch(id, source, false, LampEvent.Of(status));
		}

		public void HandleLampEvent(string id, Color color)
		{
			HandleLampEvent(id, color, LampSource.Lamp);
		}

		public void HandleLampEvent(string id, Color color, LampSource source)
		{
			Dispatch(id, source, false, LampEvent.Of(color));
		}

		public void HandleCoilEvent(string id, bool isEnabled)
		{
			Dispatch(id, LampSource.Lamp, true, LampEvent.Of(isEnabled ? LampStatus.On : LampStatus.Off));
		}

		/// <summary>
		/// Hands an event to every lamp its ID is mapped to, or keeps its state if
		/// none is: an ID may drive several devices, or several bulbs of one.
		/// </summary>
		private void Dispatch(string id, LampSource lampSource, bool isCoil, in LampEvent lampEvent)
		{
			if (_lampAssignments.TryGetValue(id, out var mappings)) {
				var applied = false;
				foreach (var mapping in mappings) {
					if (mapping.Source != lampSource || mapping.IsCoil != isCoil) {
						// so, if we have a coil here that happens to have the same name as a lamp,
						// or a GI light with the same name as an other lamp, skip.
						continue;
					}
					if (_mappedLamps.TryGetValue(mapping, out var lamp)) {
						ApplyEvent(id, lampEvent, lamp, mapping);
						applied = true;
					}
				}
				LogLampEvent(id, lampSource, isCoil, applied, mapped: true);

#if UNITY_EDITOR
				RefreshUI();
#endif
			} else {
				LampStates.TryAdd(id, LampState.Default);
				ApplyEvent(id, lampEvent, null, null);
				LogLampEvent(id, lampSource, isCoil, true, mapped: false);
			}
		}

		private void ApplyEvent(string id, in LampEvent lampEvent, IApiLamp? lamp, LampMapping? mapping)
		{
			var state = LampStates[id];
			switch (lampEvent.Kind) {
				case LampEventKind.Value:
					ApplyValue(id, lampEvent.Value, state, lamp, mapping);
					break;
				case LampEventKind.Status:
					ApplyStatus(id, lampEvent.Status, state, lamp);
					break;
				case LampEventKind.Color:
					ApplyColor(id, lampEvent.Color, state, lamp);
					break;
			}
		}

		private void LogLampEvent(string id, LampSource source, bool isCoil, bool applied, bool mapped)
		{
			if (!_logLampEvents) {
				return;
			}

			if (_loggedLampEvents < MaxLoggedLampEvents) {
				_loggedLampEvents++;
				if (Logger.IsDebugEnabled) {
					Logger.Debug("Lamp event sample #{Sample}: id={LampId}, source={Source}, isCoil={IsCoil}, mapped={Mapped}, applied={Applied}.",
						_loggedLampEvents, id, source, isCoil, mapped, applied);
				}
			}

			if (mapped || _loggedUnmappedLampEvents >= MaxLoggedUnmappedLampEvents || !_loggedUnmappedLampIds.Add($"{source}:{isCoil}:{id}")) {
				return;
			}

			_loggedUnmappedLampEvents++;
			Logger.Warn("Unmapped lamp {LampId} (source {Source}, coil {IsCoil}) changed, ignoring it.", id, source, isCoil);
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
					var color = ComposeRgbMulti(mapping, state);
					// RGB channels carry colour, while an optional alpha channel carries
					// intensity. Without alpha, the composed colour has full intensity.
					lamp?.OnLamp(color.a);
					lamp?.OnLamp(color);
					break;

				case LampType.SingleFading:
					state.Intensity = value / mapping.FadingSteps;
					LampStates[id] = state;
					lamp?.OnLamp(state.Intensity);
					break;

				default:
					if (_unknownLampTypeLog.ShouldLog(id)) {
						Logger.Warn("Unknown mapping type {Type} of lamp {LampId}.", mapping.Type, id);
					}
					break;
			}
		}

		/// <summary>
		/// Builds the light's colour out of every channel mapped to it. Each channel keeps
		/// its own state, so reading one state's colour would give the channel that was
		/// just written plus two stale ones. Only the lamp's own channels are read, each
		/// for the one component it drives: a state shared with another lamp's channel,
		/// where one ID drives both, carries that channel too, and it must not leak in.
		/// Channels the lamp lacks are black, and its intensity full.
		/// </summary>
		private UnityEngine.Color ComposeRgbMulti(LampMapping mapping, LampState writtenState)
		{
			if (!_mappedLamps.TryGetValue(mapping, out var lamp) || !_rgbMultiChannels.TryGetValue(lamp, out var channels)) {
				return writtenState.Color.ToUnityColor();
			}

			// Color is a reference type, so nothing here may assign to it - that would
			// write back into the state we are reading.
			var r = 0f;
			var g = 0f;
			var b = 0f;
			var a = 1f;
			foreach (var channel in channels) {
				if (!LampStates.TryGetValue(channel.Id, out var channelState)) {
					continue;
				}
				switch (channel.Channel) {
					case ColorChannel.Red:
						r = channelState.Color.R;
						break;
					case ColorChannel.Green:
						g = channelState.Color.G;
						break;
					case ColorChannel.Blue:
						b = channelState.Color.B;
						break;
					case ColorChannel.Alpha:
						a = channelState.Color.A;
						break;
				}
			}
			return new UnityEngine.Color(r, g, b, a);
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
				_lampAssignments[id] = new List<LampMapping>();
			}
			_lampAssignments[id].Add(lampMapping);
			if (_lamps.TryGetValue(lampMapping.Device, out var api)) {
				_mappedLamps[lampMapping] = ItemLamp(api, lampMapping.DeviceItem);
			}
			LampStates[id] = new LampState(lampMapping.Device.LampStatus, lampMapping.Device.LampColor.ToEngineColor());
		}

		private enum LampEventKind
		{
			Value,
			Status,
			Color,
		}

		/// <summary>What a lamp event carries: a value, a status or a colour.</summary>
		private readonly struct LampEvent
		{
			public readonly LampEventKind Kind;
			public readonly float Value;
			public readonly LampStatus Status;
			public readonly Color Color;

			private LampEvent(LampEventKind kind, float value, LampStatus status, Color color)
			{
				Kind = kind;
				Value = value;
				Status = status;
				Color = color;
			}

			public static LampEvent Of(float value) => new(LampEventKind.Value, value, LampStatus.Off, null!);
			public static LampEvent Of(LampStatus status) => new(LampEventKind.Status, 0f, status, null!);
			public static LampEvent Of(Color color) => new(LampEventKind.Color, 0f, LampStatus.Off, color);
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
