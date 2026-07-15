// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace VisualPinball.Unity.Editor
{
	public readonly struct DmdCueSimulationSample
	{
		public double Time { get; }
		public DmdCuePlayerSnapshot Snapshot { get; }

		public DmdCueSimulationSample(double time, DmdCuePlayerSnapshot snapshot)
		{
			Time = time;
			Snapshot = snapshot;
		}
	}

	public sealed class DmdCueSimulationResult
	{
		public IReadOnlyList<DmdCueSimulationSample> Samples { get; }
		public IReadOnlyList<string> Errors { get; }
		public int EmittedFrames { get; }

		internal DmdCueSimulationResult(IReadOnlyList<DmdCueSimulationSample> samples,
			IReadOnlyList<string> errors, int emittedFrames)
		{
			Samples = samples;
			Errors = errors;
			EmittedFrames = emittedFrames;
		}
	}

	/// <summary>
	/// Runs editor-authored events through the production cue player and scheduler.
	/// </summary>
	public static class DmdCueSimulator
	{
		public static DmdCueSimulationResult Run(DmdProjectAsset project, string script, double durationSeconds)
		{
			if (project == null) throw new ArgumentNullException(nameof(project));
			if (double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds <= 0d) {
				throw new ArgumentOutOfRangeException(nameof(durationSeconds));
			}
			var errors = new List<string>();
			var events = Parse(script, errors);
			var samples = new List<DmdCueSimulationSample>();
			var sink = new SimulatorSink();
			using (var player = new DmdCuePlayer(project, sink)) {
				player.OnValidationError += (_, message) => errors.Add(message);
				var eventIndex = 0;
				while (eventIndex < events.Count && events[eventIndex].Time <= 0d) {
					Dispatch(player, events[eventIndex++], errors);
				}
				player.Start();
				var frameRate = System.Math.Max(1, System.Math.Min(DmdValidation.MaxFrameRate, project.FrameRate));
				var frames = System.Math.Max(1, (int)System.Math.Ceiling(durationSeconds * frameRate));
				for (var frame = 0; frame <= frames; frame++) {
					var time = System.Math.Min(durationSeconds, frame / (double)frameRate);
					while (eventIndex < events.Count && events[eventIndex].Time <= time + 0.0000001d) {
						Dispatch(player, events[eventIndex++], errors);
					}
					player.Tick(time);
					samples.Add(new DmdCueSimulationSample(time, player.GetSnapshot()));
				}
			}
			return new DmdCueSimulationResult(samples, errors.Distinct().ToArray(), sink.FrameCount);
		}

		private static List<SimulatorEvent> Parse(string script, List<string> errors)
		{
			var events = new List<SimulatorEvent>();
			var lines = (script ?? string.Empty).Replace("\r", string.Empty).Split('\n');
			for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
				var line = lines[lineIndex].Trim();
				if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
				try {
					var separator = line.IndexOf(' ');
					if (!line.StartsWith("t=", StringComparison.OrdinalIgnoreCase) || separator < 3 ||
					    !double.TryParse(line.Substring(2, separator - 2), NumberStyles.Float,
						    CultureInfo.InvariantCulture, out var time) || time < 0d) {
						throw new FormatException("Expected a non-negative 't=<seconds>' prefix.");
					}
					var call = line.Substring(separator + 1).Trim();
					var open = call.IndexOf('(');
					var close = call.LastIndexOf(')');
					if (open <= 0 || close != call.Length - 1) throw new FormatException("Expected Action(...). ");
					var action = call.Substring(0, open).Trim();
					var arguments = SplitArguments(call.Substring(open + 1, close - open - 1));
					var parsedAction = ParseAction(action);
					var cueId = parsedAction == SimulatorAction.StopAll
						? null
						: arguments.FirstOrDefault()?.Trim();
					if (parsedAction != SimulatorAction.StopAll && string.IsNullOrEmpty(cueId)) {
						throw new FormatException($"{parsedAction} requires a cue id.");
					}
					var values = new List<DmdParamValue>();
					for (var index = cueId == null ? 0 : 1; index < arguments.Count; index++) {
						values.Add(ParseParameter(arguments[index]));
					}
					events.Add(new SimulatorEvent(time, parsedAction, cueId, values, lineIndex + 1));
				} catch (Exception exception) when (exception is FormatException || exception is ArgumentException) {
					errors.Add($"Line {lineIndex + 1}: {exception.Message}");
				}
			}
			events.Sort((left, right) => left.Time != right.Time
				? left.Time.CompareTo(right.Time)
				: left.Line.CompareTo(right.Line));
			return events;
		}

		private static List<string> SplitArguments(string input)
		{
			var result = new List<string>();
			var start = 0;
			var quoted = false;
			for (var index = 0; index < input.Length; index++) {
				if (input[index] == '"' && (index == 0 || input[index - 1] != '\\')) quoted = !quoted;
				if (input[index] == ',' && !quoted) {
					result.Add(input.Substring(start, index - start).Trim());
					start = index + 1;
				}
			}
			if (quoted) throw new FormatException("Unterminated quoted string.");
			if (start < input.Length) result.Add(input.Substring(start).Trim());
			return result;
		}

		private static DmdParamValue ParseParameter(string argument)
		{
			var separator = argument.IndexOf('=');
			if (separator <= 0) throw new FormatException($"Parameter '{argument}' must be name=value.");
			var name = argument.Substring(0, separator).Trim();
			var value = argument.Substring(separator + 1).Trim();
			if (!DmdValidation.IsValidParameterName(name)) throw new FormatException($"Invalid parameter name '{name}'.");
			if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"') {
				return DmdParamValue.From(name, value.Substring(1, value.Length - 2).Replace("\\\"", "\""));
			}
			if (bool.TryParse(value, out var boolean)) return DmdParamValue.From(name, boolean);
			if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) {
				return DmdParamValue.From(name, integer);
			}
			if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) {
				return DmdParamValue.From(name, number);
			}
			return DmdParamValue.From(name, value);
		}

		private static SimulatorAction ParseAction(string value)
		{
			if (Enum.TryParse(value, true, out SimulatorAction action)) return action;
			throw new FormatException($"Unknown simulator action '{value}'.");
		}

		private static void Dispatch(DmdCuePlayer player, SimulatorEvent evt, List<string> errors)
		{
			try {
				var parameters = ToParams(evt.Values);
				switch (evt.Action) {
					case SimulatorAction.SetBase: player.SetBase(evt.CueId, parameters); break;
					case SimulatorAction.Play: player.Play(evt.CueId, parameters); break;
					case SimulatorAction.Update:
						if (!player.UpdateCue(evt.CueId, parameters)) errors.Add($"Line {evt.Line}: cue '{evt.CueId}' is not live.");
						break;
					case SimulatorAction.Stop:
						if (!player.StopCue(evt.CueId)) errors.Add($"Line {evt.Line}: cue '{evt.CueId}' is not live.");
						break;
					case SimulatorAction.StopAll: player.StopAll(); break;
				}
			} catch (Exception exception) when (!(exception is OutOfMemoryException)) {
				errors.Add($"Line {evt.Line}: {exception.Message}");
			}
		}

		private static DmdParams ToParams(IEnumerable<DmdParamValue> values)
		{
			var result = new DmdParams();
			foreach (var value in values) {
				switch (value.Type) {
					case DmdParamType.Integer: result.Set(value.Name, value.IntValue); break;
					case DmdParamType.Float: result.Set(value.Name, value.FloatValue); break;
					case DmdParamType.String: result.Set(value.Name, value.StringValue); break;
					case DmdParamType.Boolean: result.Set(value.Name, value.BoolValue); break;
				}
			}
			return result;
		}

		private enum SimulatorAction { SetBase, Play, Update, Stop, StopAll }

		private readonly struct SimulatorEvent
		{
			public readonly double Time;
			public readonly SimulatorAction Action;
			public readonly string CueId;
			public readonly IReadOnlyList<DmdParamValue> Values;
			public readonly int Line;

			public SimulatorEvent(double time, SimulatorAction action, string cueId,
				IReadOnlyList<DmdParamValue> values, int line)
			{
				Time = time; Action = action; CueId = cueId; Values = values; Line = line;
			}
		}

		private sealed class SimulatorSink : IDmdFrameSink
		{
			public int FrameCount { get; private set; }
			public void RequestDisplays(RequestedDisplays displays) { }
			public void UpdateFrame(DisplayFrameData frame) => FrameCount++;
			public void Clear(string displayId) { }
		}
	}

	public sealed class DmdCueSimulatorView : VisualElement
	{
		private const float LabelWidth = 58f;
		private readonly TextField _script;
		private readonly FloatField _duration;
		private readonly Label _status;
		private readonly VisualElement _lanes;
		private DmdProjectAsset _project;
		private DmdCueSimulationResult _result;

		public DmdCueSimulationResult Result => _result;

		public DmdCueSimulatorView()
		{
			_script = new TextField("Events") { multiline = true, value =
				"t=0 SetBase(attract)\nt=0 Play(multiball)\nt=0.4 Play(jackpot, value=100000)" };
			_script.style.minHeight = 62;
			Add(_script);
			var controls = new VisualElement { style = { flexDirection = FlexDirection.Row } };
			_duration = new FloatField("Seconds") { value = 4f };
			_duration.style.width = 150;
			controls.Add(_duration);
			var run = new Button(Run) { text = "Run Simulation" };
			controls.Add(run);
			_status = new Label();
			_status.style.flexGrow = 1;
			_status.style.unityTextAlign = TextAnchor.MiddleLeft;
			controls.Add(_status);
			Add(controls);
			_lanes = new VisualElement { name = "dmd-simulator-lanes" };
			_lanes.style.height = 98;
			_lanes.generateVisualContent += DrawLanes;
			var laneLabels = new[] { "Base", "Active", "Held", "Queue" };
			for (var index = 0; index < laneLabels.Length; index++) {
				var label = new Label(laneLabels[index]) { pickingMode = PickingMode.Ignore };
				label.style.position = Position.Absolute;
				label.style.left = 4f;
				label.style.top = index * 24.5f;
				label.style.width = LabelWidth - 8f;
				label.style.height = 24.5f;
				label.style.unityTextAlign = TextAnchor.MiddleLeft;
				_lanes.Add(label);
			}
			Add(_lanes);
		}

		public void SetProject(DmdProjectAsset project)
		{
			_project = project;
			_result = null;
			_status.text = project == null ? "Select a project." : string.Empty;
			_lanes.MarkDirtyRepaint();
		}

		public void SetScript(string script)
		{
			_script.SetValueWithoutNotify(script ?? string.Empty);
		}

		public void Run()
		{
			if (_project == null) {
				_status.text = "Select a project.";
				return;
			}
			try {
				_result = DmdCueSimulator.Run(_project, _script.value, System.Math.Max(0.1f, _duration.value));
				_status.text = _result.Errors.Count == 0
					? $"{_result.EmittedFrames} frames"
					: $"{_result.Errors.Count} issue(s): {_result.Errors[0]}";
			} catch (Exception exception) {
				_result = null;
				_status.text = exception.Message;
			}
			_lanes.MarkDirtyRepaint();
		}

		private void DrawLanes(MeshGenerationContext context)
		{
			var painter = context.painter2D;
			var rowHeight = System.Math.Max(20f, _lanes.contentRect.height / 4f);
			for (var row = 0; row < 4; row++) {
				FillRect(painter, new Rect(0f, row * rowHeight, LabelWidth - 2f, rowHeight - 1f),
					new Color(0.16f, 0.16f, 0.16f));
			}
			if (_result?.Samples == null || _result.Samples.Count == 0) return;
			var width = System.Math.Max(1f, _lanes.contentRect.width - LabelWidth);
			for (var index = 0; index < _result.Samples.Count; index++) {
				var sample = _result.Samples[index];
				var next = index + 1 < _result.Samples.Count ? _result.Samples[index + 1].Time : sample.Time;
				var x = LabelWidth + width * (float)(sample.Time / System.Math.Max(0.1f, _duration.value));
				var right = LabelWidth + width * (float)(next / System.Math.Max(0.1f, _duration.value));
				var snapshot = sample.Snapshot;
				DrawLane(painter, 0, snapshot.BaseCueId, x, right, rowHeight);
				DrawLane(painter, 1, snapshot.ActiveCueId, x, right, rowHeight);
				DrawLane(painter, 2, string.Join(" + ", snapshot.HoldStackCueIds), x, right, rowHeight);
				DrawLane(painter, 3, string.Join(" + ", snapshot.QueuedCueIds), x, right, rowHeight);
			}
		}

		private static void DrawLane(Painter2D painter, int row, string cueId, float left, float right,
			float rowHeight)
		{
			if (string.IsNullOrEmpty(cueId)) return;
			var hash = cueId.Aggregate(17, (current, character) => current * 31 + character);
			var hue = (hash & 1023) / 1023f;
			FillRect(painter, new Rect(left, row * rowHeight + 2f, System.Math.Max(1f, right - left + 0.5f),
				rowHeight - 4f), Color.HSVToRGB(hue, 0.58f, 0.78f));
		}

		private static void FillRect(Painter2D painter, Rect rect, Color color)
		{
			painter.fillColor = color;
			painter.BeginPath();
			painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
			painter.LineTo(new Vector2(rect.xMax, rect.yMin));
			painter.LineTo(new Vector2(rect.xMax, rect.yMax));
			painter.LineTo(new Vector2(rect.xMin, rect.yMax));
			painter.ClosePath();
			painter.Fill();
		}
	}
}
