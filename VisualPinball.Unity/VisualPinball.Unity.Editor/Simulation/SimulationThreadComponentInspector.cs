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
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SimulationThreadComponent)), CanEditMultipleObjects]
	public sealed class SimulationThreadComponentInspector : UnityEditor.Editor
	{
		private string _statusMessage;
		private MessageType _statusType = MessageType.Info;

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

		private void DrawDevices(System.Collections.Generic.IReadOnlyList<NativeInputDeviceInfo> devices)
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

		private static string DeviceLabel(NativeInputDeviceInfo device)
		{
			var name = string.IsNullOrEmpty(device.Name) ? device.Id : device.Name;
			var state = device.IsConnected ? "connected" : "disconnected";
			return $"{name} ({state})";
		}

		private static string AxisLabel(NativeInputAxisInfo axis)
		{
			var name = string.IsNullOrEmpty(axis.Name) ? $"Axis {axis.AxisId}" : axis.Name;
			return $"{name}: {axis.RawValue:+0.000;-0.000;0.000} [{axis.Kind}]";
		}

		private void SetStatus(string message, MessageType type)
		{
			_statusMessage = message;
			_statusType = type;
		}
	}
}
