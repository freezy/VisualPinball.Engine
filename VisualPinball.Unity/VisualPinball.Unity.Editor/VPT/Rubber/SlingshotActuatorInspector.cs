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
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SlingshotActuatorAsset))]
	public sealed class SlingshotActuatorInspector : UnityEditor.Editor
	{
		private static readonly string[] ScalarFields = {
			"SupplyVoltage", "CoilResistanceOhm", "TurnOffClampVoltage",
			"StrokeMeters", "MaximumCurrentAmps", "EffectiveMovingMassKg",
			"ReturnSpringNewtonPerMeter", "ReturnSpringPreloadNewton",
			"ViscousDampingNewtonSecondPerMeter", "EndStopStiffnessNewtonPerMeter",
			"EndStopDampingNewtonSecondPerMeter",
		};

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			foreach (var field in ScalarFields) {
				EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
			}
			serializedObject.ApplyModifiedProperties();

			var actuator = (SlingshotActuatorAsset)target;
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Calibration LUT", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("Grid",
				$"{actuator.CurrentSampleCount} current x {actuator.StrokeSampleCount} stroke samples");
			if (GUILayout.Button("Import current, stroke, force, flux CSV")) {
				ImportCsv(actuator);
			}
			foreach (var error in actuator.ValidateData()) {
				EditorGUILayout.HelpBox(error, MessageType.Error);
			}
			DrawHeatMap("Force (N)", actuator.ForceNewtonLut,
				actuator.CurrentSampleCount, actuator.StrokeSampleCount);
			DrawHeatMap("Flux linkage (Wb-turn)", actuator.FluxLinkageWeberTurnLut,
				actuator.CurrentSampleCount, actuator.StrokeSampleCount);
		}

		private static void ImportCsv(SlingshotActuatorAsset actuator)
		{
			var path = EditorUtility.OpenFilePanel("Import slingshot actuator calibration",
				string.Empty, "csv");
			if (string.IsNullOrEmpty(path)) {
				return;
			}
			string csv;
			try {
				csv = File.ReadAllText(path);
			} catch (Exception exception) when (exception is IOException
				or UnauthorizedAccessException) {
				EditorUtility.DisplayDialog("Cannot read actuator CSV", exception.Message, "OK");
				return;
			}
			if (!SlingshotActuatorCsv.TryParse(csv, out var data,
				out var error)) {
				EditorUtility.DisplayDialog("Invalid actuator CSV", error, "OK");
				return;
			}
			Undo.RecordObject(actuator, "Import Slingshot Actuator Calibration");
			actuator.MaximumCurrentAmps = data.MaximumCurrentAmps;
			actuator.StrokeMeters = data.StrokeMeters;
			actuator.CurrentSampleCount = data.CurrentSampleCount;
			actuator.StrokeSampleCount = data.StrokeSampleCount;
			actuator.ForceNewtonLut = data.ForceNewton;
			actuator.FluxLinkageWeberTurnLut = data.FluxLinkageWeberTurn;
			EditorUtility.SetDirty(actuator);
		}

		private static void DrawHeatMap(string label, float[] values, int rows, int columns)
		{
			if (values == null || rows < 1 || columns < 1 || values.Length != rows * columns
				|| values.Any(value => !float.IsFinite(value))) {
				return;
			}
			EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
			var rect = GUILayoutUtility.GetRect(0f, Mathf.Min(120f, rows * 8f),
				GUILayout.ExpandWidth(true));
			var minimum = values.Min();
			var maximum = values.Max();
			for (var row = 0; row < rows; row++) {
				for (var column = 0; column < columns; column++) {
					var value = values[row * columns + column];
					var normalized = maximum > minimum
						? Mathf.InverseLerp(minimum, maximum, value)
						: 0f;
					var cell = new Rect(
						rect.x + column * rect.width / columns,
						rect.y + (rows - row - 1) * rect.height / rows,
						rect.width / columns + 1f,
						rect.height / rows + 1f);
					EditorGUI.DrawRect(cell, Color.Lerp(new Color(0.05f, 0.15f, 0.45f),
						new Color(1f, 0.35f, 0.05f), normalized));
				}
			}
			EditorGUILayout.LabelField($"{minimum:g5} .. {maximum:g5}", EditorStyles.miniLabel);
		}
	}

	public readonly struct SlingshotActuatorCsvData
	{
		public readonly int CurrentSampleCount;
		public readonly int StrokeSampleCount;
		public readonly float MaximumCurrentAmps;
		public readonly float StrokeMeters;
		public readonly float[] ForceNewton;
		public readonly float[] FluxLinkageWeberTurn;

		public SlingshotActuatorCsvData(int currentSampleCount, int strokeSampleCount,
			float maximumCurrentAmps, float strokeMeters, float[] forceNewton,
			float[] fluxLinkageWeberTurn)
		{
			CurrentSampleCount = currentSampleCount;
			StrokeSampleCount = strokeSampleCount;
			MaximumCurrentAmps = maximumCurrentAmps;
			StrokeMeters = strokeMeters;
			ForceNewton = forceNewton;
			FluxLinkageWeberTurn = fluxLinkageWeberTurn;
		}
	}

	public static class SlingshotActuatorCsv
	{
		private readonly struct Row
		{
			public readonly float Current;
			public readonly float Stroke;
			public readonly float Force;
			public readonly float Flux;

			public Row(float current, float stroke, float force, float flux)
			{
				Current = current;
				Stroke = stroke;
				Force = force;
				Flux = flux;
			}
		}

		public static bool TryParse(string csv, out SlingshotActuatorCsvData data,
			out string error)
		{
			data = default;
			var lines = (csv ?? string.Empty).Split(new[] { '\r', '\n' },
				StringSplitOptions.RemoveEmptyEntries);
			if (lines.Length < 5) {
				error = "CSV needs a header and a rectangular grid with at least 2 x 2 samples.";
				return false;
			}
			var header = lines[0].Split(',').Select(value => value.Trim().ToLowerInvariant()).ToArray();
			var expectedHeader = new[] { "current_amps", "stroke_meters", "force_newton", "flux_weber_turn" };
			if (!header.SequenceEqual(expectedHeader)) {
				error = $"Expected header: {string.Join(",", expectedHeader)}";
				return false;
			}

			var rows = new List<Row>(lines.Length - 1);
			for (var line = 1; line < lines.Length; line++) {
				var fields = lines[line].Split(',');
				if (fields.Length != 4
					|| !fields.All(field => float.TryParse(field.Trim(), NumberStyles.Float,
						CultureInfo.InvariantCulture, out _))) {
					error = $"Line {line + 1} must contain four finite numbers.";
					return false;
				}
				var values = fields.Select(field => float.Parse(field.Trim(),
					NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
				if (values.Any(value => !float.IsFinite(value))) {
					error = $"Line {line + 1} contains a non-finite number.";
					return false;
				}
				rows.Add(new Row(values[0], values[1], values[2], values[3]));
			}

			var currents = rows.Select(row => row.Current).Distinct().OrderBy(value => value).ToArray();
			var strokes = rows.Select(row => row.Stroke).Distinct().OrderBy(value => value).ToArray();
			if (currents.Length < 2 || strokes.Length < 2
				|| rows.Count != currents.Length * strokes.Length) {
				error = "CSV must contain one sample for every point of a grid at least 2 x 2.";
				return false;
			}
			if (Mathf.Abs(currents[0]) > 1e-7f || Mathf.Abs(strokes[0]) > 1e-7f
				|| currents[^1] <= 0f || strokes[^1] <= 0f) {
				error = "Current and stroke grids must start at zero and end at positive maxima.";
				return false;
			}
			if (!IsUniform(currents) || !IsUniform(strokes)) {
				error = "Current and stroke sample coordinates must be uniformly spaced.";
				return false;
			}

			var force = new float[rows.Count];
			var flux = new float[rows.Count];
			var assigned = new bool[rows.Count];
			foreach (var row in rows) {
				var current = Array.IndexOf(currents, row.Current);
				var stroke = Array.IndexOf(strokes, row.Stroke);
				var index = current * strokes.Length + stroke;
				if (assigned[index]) {
					error = "CSV contains a duplicate current/stroke coordinate.";
					return false;
				}
				assigned[index] = true;
				force[index] = row.Force;
				flux[index] = row.Flux;
			}
			data = new SlingshotActuatorCsvData(currents.Length, strokes.Length,
				currents[^1], strokes[^1], force, flux);
			error = null;
			return true;
		}

		private static bool IsUniform(IReadOnlyList<float> values)
		{
			var interval = values[^1] / (values.Count - 1);
			for (var i = 1; i < values.Count; i++) {
				if (Mathf.Abs(values[i] - interval * i) > Mathf.Max(1e-7f, interval * 1e-5f)) {
					return false;
				}
			}
			return true;
		}
	}
}
