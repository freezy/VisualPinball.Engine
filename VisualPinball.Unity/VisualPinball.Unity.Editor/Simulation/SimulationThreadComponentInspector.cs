// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SimulationThreadComponent)), CanEditMultipleObjects]
	public sealed class SimulationThreadComponentInspector : UnityEditor.Editor
	{
		private const int GraphSampleCount = 120;
		private const int GraphMaxAxes = 6;

		private static readonly Color[] GraphColors = {
			new Color(0.95f, 0.32f, 0.28f),
			new Color(0.24f, 0.62f, 1f),
			new Color(0.34f, 0.82f, 0.42f),
			new Color(1f, 0.72f, 0.2f),
			new Color(0.74f, 0.46f, 1f),
			new Color(0.25f, 0.86f, 0.8f)
		};

		private readonly Dictionary<string, AxisGraphState> _axisGraphs = new Dictionary<string, AxisGraphState>();
		private string _statusMessage;
		private MessageType _statusType = MessageType.Info;
		private GUIStyle _graphLegendStyle;
		private double _lastGraphSampleTime;

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			EditorGUI.BeginChangeCheck();
			DrawPropertiesExcluding(serializedObject, "m_Script");
			var changed = EditorGUI.EndChangeCheck();
			serializedObject.ApplyModifiedProperties();

			if (targets.Length == 1 && target is SimulationThreadComponent component) {
				if (changed && Application.isPlaying) {
					component.ApplyNudgeSensorSettings();
				}
				DrawNudgeCalibration(component);
			}
		}

		public override bool RequiresConstantRepaint()
		{
			return Application.isPlaying && targets.Length == 1;
		}

		private void DrawNudgeCalibration(SimulationThreadComponent component)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Nudge Calibration", EditorStyles.boldLabel);

			if (!Application.isPlaying) {
				EditorGUILayout.HelpBox("Enter Play Mode to read native input devices and calibrate the current sensor centers.", MessageType.Info);
				return;
			}

			EditorGUILayout.LabelField("Simulation", component.IsRunning ? "Running" : "Stopped");
			EditorGUILayout.LabelField("Input Thread", $"{component.InputThreadActualHz:0.#} Hz / target {component.InputThreadTargetHz:0.#} Hz");

			var devices = component.ListNudgeInputDevices();
			if (devices.Count == 0) {
				EditorGUILayout.HelpBox("No native input devices are visible. Check that native input is enabled and that the device was connected before polling started.", MessageType.Warning);
			} else {
				DrawDevices(devices);
				DrawInputGraph(component, devices);
			}

			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();
			EditorGUI.BeginDisabledGroup(devices.Count == 0);
			if (GUILayout.Button("Auto-map First Device")) {
				Undo.RecordObject(component, "Auto-map nudge sensor");
				if (component.TryAutoConfigureFirstCabinetSensor(out var message)) {
					SetStatus(message, MessageType.Info);
					EditorUtility.SetDirty(component);
				} else {
					SetStatus(message, MessageType.Warning);
				}
			}
			EditorGUI.EndDisabledGroup();

			EditorGUI.BeginDisabledGroup(devices.Count == 0 || component.NudgeSensors == null || component.NudgeSensors.Count == 0);
			if (GUILayout.Button("Calibrate Centers")) {
				Undo.RecordObject(component, "Calibrate nudge sensor centers");
				var calibrated = component.CalibrateNudgeSensorCenters();
				if (calibrated > 0) {
					SetStatus($"Calibrated {calibrated} mapped nudge channel(s).", MessageType.Info);
					EditorUtility.SetDirty(component);
				} else {
					SetStatus("No mapped channels matched the currently visible devices.", MessageType.Warning);
				}
			}
			EditorGUI.EndDisabledGroup();
			EditorGUILayout.EndHorizontal();

			EditorGUI.BeginDisabledGroup(component.NudgeSensors == null || component.NudgeSensors.Count == 0);
			if (GUILayout.Button("Reset Centers")) {
				Undo.RecordObject(component, "Reset nudge sensor centers");
				var reset = component.ResetNudgeSensorCenters();
				SetStatus(reset > 0 ? "Cleared nudge sensor raw centers." : "No nudge sensors to reset.", MessageType.Info);
				EditorUtility.SetDirty(component);
			}
			EditorGUI.EndDisabledGroup();

			if (!string.IsNullOrEmpty(_statusMessage)) {
				EditorGUILayout.HelpBox(_statusMessage, _statusType);
			}
		}

		private void DrawDevices(IReadOnlyList<NativeInputDeviceInfo> devices)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Native Devices", EditorStyles.boldLabel);
			for (var i = 0; i < devices.Count; i++) {
				var device = devices[i];
				EditorGUILayout.LabelField(DeviceLabel(device), EditorStyles.miniBoldLabel);
				if (device.Axes == null || device.Axes.Count == 0) {
					EditorGUILayout.LabelField("  No axes");
					continue;
				}
				var axisCount = System.Math.Min(device.Axes.Count, 6);
				for (var j = 0; j < axisCount; j++) {
					var axis = device.Axes[j];
					EditorGUILayout.LabelField($"  {AxisLabel(axis)}", EditorStyles.miniLabel);
				}
				if (device.Axes.Count > axisCount) {
					EditorGUILayout.LabelField($"  ... {device.Axes.Count - axisCount} more", EditorStyles.miniLabel);
				}
			}
		}

		private void DrawInputGraph(SimulationThreadComponent component, IReadOnlyList<NativeInputDeviceInfo> devices)
		{
			var channels = new List<InputGraphChannel>();
			var title = "Nudge Graph: Mounted Mappings";
			CollectMappedGraphChannels(component, devices, channels);
			if (channels.Count == 0) {
				if (!TryFindGraphDevice(devices, out var device)) {
					return;
				}
				title = $"Raw Input Graph: {DeviceName(device)}";
				CollectRawGraphChannels(device, channels);
			}
			if (channels.Count == 0) {
				return;
			}

			SampleGraphChannels(channels);

			EditorGUILayout.Space();
			EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

			var rect = GUILayoutUtility.GetRect(1f, 72f, GUILayout.ExpandWidth(true));
			DrawGraphFrame(rect);

			for (var i = 0; i < channels.Count; i++) {
				if (_axisGraphs.TryGetValue(channels[i].Key, out var graph)) {
					DrawGraphLine(rect, graph, GraphColors[i % GraphColors.Length]);
				}
			}

			DrawGraphLegend(channels);
		}

		private void SampleGraphChannels(IReadOnlyList<InputGraphChannel> channels)
		{
			var now = EditorApplication.timeSinceStartup;
			if (now - _lastGraphSampleTime < 1.0 / 60.0) {
				return;
			}
			_lastGraphSampleTime = now;

			for (var i = 0; i < channels.Count; i++) {
				var channel = channels[i];
				if (!_axisGraphs.TryGetValue(channel.Key, out var graph)) {
					graph = new AxisGraphState();
					_axisGraphs[channel.Key] = graph;
				}
				graph.Add(channel.Value);
			}
		}

		private void DrawGraphFrame(Rect rect)
		{
			EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f));

			Handles.BeginGUI();
			var previousColor = Handles.color;
			Handles.color = new Color(1f, 1f, 1f, 0.18f);
			Handles.DrawLine(new Vector3(rect.xMin, rect.center.y), new Vector3(rect.xMax, rect.center.y));
			Handles.color = new Color(1f, 1f, 1f, 0.08f);
			Handles.DrawLine(new Vector3(rect.xMin, rect.yMin + rect.height * 0.25f), new Vector3(rect.xMax, rect.yMin + rect.height * 0.25f));
			Handles.DrawLine(new Vector3(rect.xMin, rect.yMin + rect.height * 0.75f), new Vector3(rect.xMax, rect.yMin + rect.height * 0.75f));
			Handles.color = previousColor;
			Handles.EndGUI();
		}

		private static void DrawGraphLine(Rect rect, AxisGraphState graph, Color color)
		{
			if (graph.Count < 2) {
				return;
			}

			Handles.BeginGUI();
			var previousColor = Handles.color;
			Handles.color = color;
			var previous = GraphPoint(rect, graph, 0);
			for (var i = 1; i < graph.Count; i++) {
				var next = GraphPoint(rect, graph, i);
				Handles.DrawLine(previous, next);
				previous = next;
			}
			Handles.color = previousColor;
			Handles.EndGUI();
		}

		private static Vector3 GraphPoint(Rect rect, AxisGraphState graph, int index)
		{
			var x = graph.Count <= 1 ? rect.xMin : Mathf.Lerp(rect.xMin, rect.xMax, index / (float)(graph.Count - 1));
			var value = Mathf.Clamp(graph.Get(index), -1f, 1f);
			var y = rect.center.y - value * rect.height * 0.45f;
			return new Vector3(x, y);
		}

		private void DrawGraphLegend(IReadOnlyList<InputGraphChannel> channels)
		{
			if (_graphLegendStyle == null) {
				_graphLegendStyle = new GUIStyle(EditorStyles.miniLabel) {
					richText = true,
					wordWrap = false
				};
			}

			EditorGUILayout.BeginHorizontal();
			for (var i = 0; i < channels.Count; i++) {
				var color = GraphColors[i % GraphColors.Length];
				var colorHex = ColorUtility.ToHtmlStringRGB(color);
				GUILayout.Label($"<color=#{colorHex}>{channels[i].Label} {channels[i].Value:+0.00;-0.00;0.00}</color>", _graphLegendStyle);
			}
			EditorGUILayout.EndHorizontal();
		}

		private static void CollectMappedGraphChannels(SimulationThreadComponent component,
			IReadOnlyList<NativeInputDeviceInfo> devices, List<InputGraphChannel> channels)
		{
			if (component.NudgeSensors == null) {
				return;
			}

			for (var sensorIndex = 0; sensorIndex < component.NudgeSensors.Count && channels.Count < GraphMaxAxes; sensorIndex++) {
				var sensor = component.NudgeSensors[sensorIndex];
				if (sensor == null) {
					continue;
				}
				AddMappedGraphChannel(sensorIndex, sensor, NudgeSensorChannel.X, sensor.X, devices, channels);
				AddMappedGraphChannel(sensorIndex, sensor, NudgeSensorChannel.Y, sensor.Y, devices, channels);
				AddMappedGraphChannel(sensorIndex, sensor, NudgeSensorChannel.VelocityX, sensor.VelocityX, devices, channels);
				AddMappedGraphChannel(sensorIndex, sensor, NudgeSensorChannel.VelocityY, sensor.VelocityY, devices, channels);
				AddMappedGraphChannel(sensorIndex, sensor, NudgeSensorChannel.AccelerationX, sensor.AccelerationX, devices, channels);
				AddMappedGraphChannel(sensorIndex, sensor, NudgeSensorChannel.AccelerationY, sensor.AccelerationY, devices, channels);
			}
		}

		private static void AddMappedGraphChannel(int sensorIndex, SimulationThreadNudgeSensorConfig sensor,
			NudgeSensorChannel sourceChannel, string mappingValue,
			IReadOnlyList<NativeInputDeviceInfo> devices, List<InputGraphChannel> channels)
		{
			if (channels.Count >= GraphMaxAxes || !SensorMapping.TryParse(mappingValue, out var mapping)) {
				return;
			}
			if (!TryFindAxis(devices, mapping.DeviceId, mapping.AxisId, out var axis)) {
				return;
			}

			var channel = sourceChannel;
			var value = CalculateMappedGraphValue(mapping, axis.RawValue);
			NudgeSensorMountTransform.TransformChannel(ref channel, ref value, sensor.MountRotation, sensor.MountMirror);

			channels.Add(new InputGraphChannel(
				$"mapped:{sensorIndex}:{sourceChannel}:{channel}:{mapping.DeviceId}:{mapping.AxisId}:{sensor.MountRotation}:{sensor.MountMirror}",
				$"{ChannelName(channel)} ({AxisName(axis)})",
				value));
		}

		private static void CollectRawGraphChannels(NativeInputDeviceInfo device, List<InputGraphChannel> channels)
		{
			var axisCount = System.Math.Min(device.Axes.Count, GraphMaxAxes);
			for (var i = 0; i < axisCount; i++) {
				var axis = device.Axes[i];
				channels.Add(new InputGraphChannel(
					GraphKey(device, axis),
					AxisName(axis),
					axis.RawValue));
			}
		}

		private static float CalculateMappedGraphValue(SensorMapping mapping, float rawValue)
		{
			var value = Mathf.Clamp(Mathf.Clamp(rawValue, -1f, 1f) - mapping.RawCenter, -1f, 1f);
			var deadZone = Mathf.Clamp(mapping.DeadZone, 0f, 0.999f);
			var absValue = Mathf.Abs(value);
			if (absValue <= deadZone) {
				return 0f;
			}
			value = Mathf.Sign(value) * ((absValue - deadZone) / (1f - deadZone));
			var limit = Mathf.Max(0f, mapping.Limit);
			return Mathf.Clamp(value, -limit, limit);
		}

		private static bool TryFindAxis(IReadOnlyList<NativeInputDeviceInfo> devices, string deviceId, int axisId,
			out NativeInputAxisInfo axis)
		{
			for (var i = 0; i < devices.Count; i++) {
				var device = devices[i];
				if (device.Id != deviceId || device.Axes == null) {
					continue;
				}
				for (var j = 0; j < device.Axes.Count; j++) {
					if (device.Axes[j].AxisId == axisId) {
						axis = device.Axes[j];
						return true;
					}
				}
			}

			axis = default;
			return false;
		}

		private static bool TryFindGraphDevice(IReadOnlyList<NativeInputDeviceInfo> devices, out NativeInputDeviceInfo device)
		{
			for (var i = 0; i < devices.Count; i++) {
				if (IsGraphableDevice(devices[i]) && IsKl25zDevice(devices[i])) {
					device = devices[i];
					return true;
				}
			}
			for (var i = 0; i < devices.Count; i++) {
				if (IsGraphableDevice(devices[i])) {
					device = devices[i];
					return true;
				}
			}

			device = default;
			return false;
		}

		private static bool IsGraphableDevice(NativeInputDeviceInfo device)
		{
			return device.IsConnected && device.Axes != null && device.Axes.Count > 0;
		}

		private static bool IsKl25zDevice(NativeInputDeviceInfo device)
		{
			return ContainsIgnoreCase(device.Name, "KL25Z")
			       || ContainsIgnoreCase(device.Id, "KL25Z")
			       || ContainsIgnoreCase(device.Name, "Pinscape")
			       || ContainsIgnoreCase(device.Id, "Pinscape");
		}

		private static bool ContainsIgnoreCase(string value, string match)
		{
			return !string.IsNullOrEmpty(value) && value.IndexOf(match, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static string GraphKey(NativeInputDeviceInfo device, NativeInputAxisInfo axis)
		{
			return $"{device.Id}:{axis.AxisId}";
		}

		private static string DeviceLabel(NativeInputDeviceInfo device)
		{
			var state = device.IsConnected ? "connected" : "disconnected";
			return $"{DeviceName(device)} ({state})";
		}

		private static string DeviceName(NativeInputDeviceInfo device)
		{
			return string.IsNullOrEmpty(device.Name) ? device.Id : device.Name;
		}

		private static string AxisLabel(NativeInputAxisInfo axis)
		{
			return $"{AxisName(axis)}: {axis.RawValue:+0.000;-0.000;0.000} [{axis.Kind}]";
		}

		private static string ChannelName(NudgeSensorChannel channel)
		{
			return channel switch {
				NudgeSensorChannel.VelocityX => "Velocity X",
				NudgeSensorChannel.VelocityY => "Velocity Y",
				NudgeSensorChannel.AccelerationX => "Accel X",
				NudgeSensorChannel.AccelerationY => "Accel Y",
				NudgeSensorChannel.Y => "Y",
				_ => "X"
			};
		}

		private static string AxisName(NativeInputAxisInfo axis)
		{
			return string.IsNullOrEmpty(axis.Name) ? $"Axis {axis.AxisId}" : axis.Name;
		}

		private void SetStatus(string message, MessageType type)
		{
			_statusMessage = message;
			_statusType = type;
		}

		private sealed class InputGraphChannel
		{
			public readonly string Key;
			public readonly string Label;
			public readonly float Value;

			public InputGraphChannel(string key, string label, float value)
			{
				Key = key;
				Label = label;
				Value = value;
			}
		}

		private sealed class AxisGraphState
		{
			private readonly float[] _samples = new float[GraphSampleCount];
			private int _next;

			public int Count { get; private set; }

			public void Add(float value)
			{
				_samples[_next] = value;
				_next = (_next + 1) % _samples.Length;
				if (Count < _samples.Length) {
					Count++;
				}
			}

			public float Get(int index)
			{
				var start = (_next - Count + _samples.Length) % _samples.Length;
				return _samples[(start + index) % _samples.Length];
			}
		}
	}
}
