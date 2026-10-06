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
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// The insert light's inspector: the lamp as previewed and mapped, and the
	/// real light that spills onto the playfield around the insert.
	/// </summary>
	[CustomEditor(typeof(InsertLightComponent))]
	[CanEditMultipleObjects]
	public class InsertLightInspector : UnityEditor.Editor
	{
		private SerializedProperty _intensity;
		private SerializedProperty _color;
		private SerializedProperty _output;
		private SerializedProperty _tintWithLamp;
		private SerializedProperty _extraBulbs;
		private SerializedProperty _bulbNames;
		private SerializedProperty _lampTint;
		private SerializedProperty _spillLumens;
		private SerializedProperty _spillRange;
		private SerializedProperty _spillLight;
		private SerializedProperty _state;
		private SerializedProperty _blinkPattern;
		private SerializedProperty _blinkInterval;
		private SerializedProperty _fadeSpeedUp;
		private SerializedProperty _fadeSpeedDown;
		private Material _checkedMaterial;
		private bool _materialReadOnly;
		private readonly Vector3[] _lampHandleOffsets = new Vector3[InsertLightComponent.MaxBulbs];

		private static readonly int LuminosityId = Shader.PropertyToID("_InsertLuminosity");
		private static readonly Color[] BulbColors = { Color.cyan, Color.yellow, Color.magenta, Color.green };
		private static GUIStyle _lampLabel;
		private static GUIStyle LampLabel => _lampLabel ??= new GUIStyle(EditorStyles.boldLabel) {
			normal = { textColor = Color.black },
		};

		private const string LampKey = "VisualPinball.InsertLight.Lamp";
		private const string SpillKey = "VisualPinball.InsertLight.Spill";
		private const string StateKey = "VisualPinball.InsertLight.State";

		private void OnEnable()
		{
			_intensity = serializedObject.FindProperty(nameof(InsertLightComponent.Intensity));
			_color = serializedObject.FindProperty(nameof(InsertLightComponent.Color));
			_output = serializedObject.FindProperty(nameof(InsertLightComponent.Output));
			_tintWithLamp = serializedObject.FindProperty(nameof(InsertLightComponent.TintWithLamp));
			_extraBulbs = serializedObject.FindProperty(nameof(InsertLightComponent.ExtraBulbs));
			_bulbNames = serializedObject.FindProperty(nameof(InsertLightComponent.BulbNames));
			_lampTint = serializedObject.FindProperty(nameof(InsertLightComponent.LampTint));
			_spillLumens = serializedObject.FindProperty(nameof(InsertLightComponent.SpillLumens));
			_spillRange = serializedObject.FindProperty(nameof(InsertLightComponent.SpillRange));
			_spillLight = serializedObject.FindProperty(nameof(InsertLightComponent.SpillLight));
			_state = serializedObject.FindProperty(nameof(InsertLightComponent.State));
			_blinkPattern = serializedObject.FindProperty(nameof(InsertLightComponent.BlinkPattern));
			_blinkInterval = serializedObject.FindProperty(nameof(InsertLightComponent.BlinkInterval));
			_fadeSpeedUp = serializedObject.FindProperty(nameof(InsertLightComponent.FadeSpeedUp));
			_fadeSpeedDown = serializedObject.FindProperty(nameof(InsertLightComponent.FadeSpeedDown));
			EditorApplication.projectChanged += InvalidateMaterialAccess;
		}

		private void OnDisable() => EditorApplication.projectChanged -= InvalidateMaterialAccess;

		private void InvalidateMaterialAccess() => _checkedMaterial = null;

		private bool MaterialIsReadOnly(Material material)
		{
			if (material == null) { return false; }
			if (_checkedMaterial != material) {
				_checkedMaterial = material;
				_materialReadOnly = InsertBaker.IsReadOnly(AssetDatabase.GetAssetPath(material));
			}
			return _materialReadOnly;
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();
			// Edited away from its lamp preset below, a bulb follows none: the dropdown says Custom.
			var matched = InsertLampPresets.Matching(targets.Cast<InsertLightComponent>());

			// Without the hybrid material there is nothing for the lamp to drive.
			var hybrid = targets.All(selected => {
				var material = ((InsertLightComponent)selected).GetComponent<MeshRenderer>().sharedMaterial;
				return material != null && material.HasProperty(LuminosityId);
			});
			if (!hybrid) {
				EditorGUILayout.HelpBox("The insert needs a hybrid bake and material before Intensity, Color and spill can work.", MessageType.Warning);
				if (targets.Length == 1) {
					var insert = ((InsertLightComponent)target).GetComponent<InsertComponent>();
					if (insert != null) {
						var blenderError = InsertBaker.Probe()?.Error;
						using (new EditorGUI.DisabledScope(InsertBaker.Baking != null || InsertBaker.IsReadOnly(insert.Bake) || EditorApplication.isPlayingOrWillChangePlaymode || Shader.Find(InsertBaker.HybridShaderName) == null || blenderError != null || insert.Error != null)) {
							if (GUILayout.Button(new GUIContent("Bake Hybrid and Use Material", "Bakes optical maps and replaces the current body material with an HDRP hybrid material. The previous material stays on disk."))) {
								InsertBaker.Bake(insert, true);
							}
						}
						if (InsertBaker.LastErrorInsert == insert && InsertBaker.LastError != null) {
							EditorGUILayout.HelpBox(InsertBaker.LastError, MessageType.Error);
						}
					}
				}
			}

			if (InsertSection.Begin(LampKey, "Lamps")) {
				using (new EditorGUI.DisabledScope(!hybrid)) {
					if (targets.Length == 1) {
						DrawLampLayout((InsertLightComponent)target);
					} else {
						Field(_intensity, "Intensity");
						DrawLampPreset();
						Field(_color, "Color");
						EditorGUILayout.HelpBox("Select one insert to edit its lamp count and positions.", MessageType.None);
					}
					Field(_tintWithLamp, "Tint With Lamp");
					using (new EditorGUI.DisabledScope(!_tintWithLamp.boolValue && !_tintWithLamp.hasMultipleDifferentValues)) {
						Field(_lampTint, "Tint Amount");
					}
				}
				if (Application.isPlaying) {
					EditorGUILayout.HelpBox("While playing, a lamp mapped in Lamp Manager or under a Light follows the game and overwrites these.", MessageType.None);
				}
			}
			InsertSection.End();

			if (InsertSection.Begin(StateKey, "State")) {
				Field(_state, "State");
				Field(_blinkPattern, "Blink Pattern");
				Field(_blinkInterval, "Blink Interval (ms)");
				Field(_fadeSpeedUp, "Fade Up (s)");
				Field(_fadeSpeedDown, "Fade Down (s)");
				EditorGUILayout.HelpBox("The status the lamp starts the game in, and how it blinks and fades when the game drives it. Under a Light, the Light's own settings apply instead.", MessageType.None);
			}
			InsertSection.End();

			if (InsertSection.Begin(SpillKey, "Spill")) {
				Field(_spillLight, "Light");
				var light = _spillLight.hasMultipleDifferentValues || _spillLight.objectReferenceValue != null;
				using (new EditorGUI.DisabledScope(!hybrid || !light)) {
					Field(_spillLumens, "Lumens");
					Field(_spillRange, "Range (m)");
				}
				if (!light) {
					EditorGUILayout.HelpBox("No spill light: the insert glows but lights nothing around it.", MessageType.None);
					using (new EditorGUI.DisabledScope(targets.Length > 1 || EditorApplication.isPlaying)) {
						if (GUILayout.Button(new GUIContent("Add Spill Light", "A wide spot light just under the insert, pointing up through it at the ball and whatever else is above, without shadows, driven with the lamp."))) {
							InsertInspector.AddLighting(((InsertLightComponent)target).GetComponent<InsertComponent>());
						}
					}
				} else if (targets.Length == 1 && hybrid) {
					var component = (InsertLightComponent)target;
					if (component.SpillLight != null && component.SpillLight.type != LightType.Spot) {
						EditorGUILayout.HelpBox("This spill is a point light, dearer than it needs to be and hard to aim. A wide spot under the insert, pointing up, does the same job.", MessageType.Info);
						using (new EditorGUI.DisabledScope(EditorApplication.isPlaying)) {
							if (GUILayout.Button(new GUIContent("Make It a Spot Light", "Turns the spill into a wide spot under the insert pointing up, with the spot's range and lumens where it still had the point light's defaults."))) {
								InsertInspector.MakeSpillSpot(component);
							}
						}
					}
					var material = component.GetComponent<MeshRenderer>().sharedMaterial;
					var lumens = _spillLumens.floatValue * Mathf.Max(0f, material.GetFloat(LuminosityId)) * Mathf.Max(0f, _output.floatValue) * _intensity.floatValue;
					EditorGUILayout.LabelField(new GUIContent("Configured Spill", "Lumens times the material's Max Brightness times the lamp's Output times the Intensity above: what the light puts out at that intensity. While playing, the game's lamp level takes the place of Intensity."), new GUIContent($"{lumens:0.###} lm"));
				}
			}
			InsertSection.End();

			serializedObject.ApplyModifiedProperties();
			InsertLampPresets.UnfollowEdited(matched);
		}

		/// <summary>
		/// The bulb's kind: a dropdown of the lamp presets, with the way back to
		/// one once the colour or fades were edited, and the way to give the
		/// whole table the same bulbs.
		/// </summary>
		private void DrawLampPreset()
		{
			var lights = targets.Cast<InsertLightComponent>().ToArray();
			if (lights.All(InsertLampPresets.IsRgb)) {
				EditorGUILayout.HelpBox("Mapped as RGB in Lamp Manager: the game drives the colour and the fading, so this is an RGB LED whatever the table's other lamps are.", MessageType.None);
				Field(_output, "Output");
				return;
			}
			var presets = InsertLampPresets.Find();
			var names = presets.Select(preset => preset.LampName).Append("Custom").ToArray();
			var first = Array.FindIndex(presets, preset => preset.LampName == lights[0].Lamp);
			var mixed = lights.Any(light => light.Lamp != lights[0].Lamp);
			var index = first >= 0 ? first : presets.Length;
			using (new EditorGUI.DisabledScope(Application.isPlaying)) {
				using (new EditorGUILayout.HorizontalScope()) {
					EditorGUI.showMixedValue = mixed;
					EditorGUI.BeginChangeCheck();
					var chosen = EditorGUILayout.Popup(new GUIContent("Lamp Preset", "The kind of bulb under the insert: its colour, how much light it gives and how it fades. The plastic's colour is the material's; this is the lamp behind it. Custom keeps the values as they are. Manage opens the Insert Presets window, where the lamps live and new ones are made."), index, names);
					EditorGUI.showMixedValue = false;
					if (EditorGUI.EndChangeCheck()) {
						InsertLampPresets.Apply(chosen < presets.Length ? presets[chosen] : null, lights);
						serializedObject.Update();
					}
					InsertPresetGui.EditButton(InsertPresetWindow.Kind.Lamp, first >= 0 && !mixed ? presets[first] : null);
				}
				Field(_output, "Output");
				var followed = first >= 0 && !mixed ? presets[first] : null;
				if (followed != null) {
					if (lights.Any(light => InsertLampPresets.Differs(light, followed))) {
						EditorGUILayout.HelpBox($"The colour, output or fades differ from the {followed.LampName} preset.", MessageType.None);
						if (GUILayout.Button(new GUIContent("Reset to Preset", "Takes the preset's colour, output and fades again."))) {
							InsertLampPresets.Apply(followed, lights);
							serializedObject.Update();
						}
					}
					if (targets.Length == 1) {
						var siblings = InsertLampPresets.Siblings(lights[0]).Where(light => !InsertLampPresets.IsRgb(light)).ToArray();
						var others = siblings.Count(light => light.Lamp != followed.LampName || InsertLampPresets.Differs(light, followed));
						using (new EditorGUI.DisabledScope(others == 0)) {
							if (GUILayout.Button(new GUIContent(others > 0 ? $"Apply to All {siblings.Length} Lamps" : "All Lamps Have This Preset", "Puts this bulb under every insert of the table: a table is lit by one kind of lamp, until it gets an LED kit."))) {
								InsertLampPresets.Apply(followed, siblings);
								serializedObject.Update();
							}
						}
					}
				} else if (!mixed && !string.IsNullOrEmpty(lights[0].Lamp)) {
					EditorGUILayout.HelpBox($"The lamp preset \"{lights[0].Lamp}\" is not in the project; the bulb keeps its values.", MessageType.None);
				}
			}
		}

		/// <summary>One compact row per active lamp: its name, drive and material position.</summary>
		private void DrawLampLayout(InsertLightComponent component)
		{
			var material = component.GetComponent<MeshRenderer>().sharedMaterial;
			var readOnly = MaterialIsReadOnly(material);
			var count = component.BulbCount;
			if (_bulbNames.arraySize < InsertLightComponent.MaxBulbs) {
				_bulbNames.arraySize = InsertLightComponent.MaxBulbs;
			}
			if (_extraBulbs.arraySize < InsertLightComponent.MaxBulbs - 1) {
				_extraBulbs.arraySize = InsertLightComponent.MaxBulbs - 1;
			}
			for (var i = 0; i < count; i++) {
				var intensity = i == 0 ? _intensity : _extraBulbs.GetArrayElementAtIndex(i - 1).FindPropertyRelative(nameof(InsertLightComponent.Bulb.Intensity));
				EditorGUI.BeginChangeCheck();
				var preview = EditorGUILayout.Slider(new GUIContent($"Lamp {i + 1}", "Preview this lamp's intensity from off to full brightness."), intensity.floatValue, 0f, 1f);
				if (EditorGUI.EndChangeCheck()) {
					intensity.floatValue = preview;
				}
			}
			DrawLampPreset();
			EditorGUILayout.Space();
			using (new EditorGUI.DisabledScope(Application.isPlaying || readOnly)) {
				EditorGUI.BeginChangeCheck();
				var wanted = EditorGUILayout.IntSlider(new GUIContent("Number of lamps", "Number of virtual lamps on this insert, from one to four. The material stores their positions and shared radius."), count, 1, InsertLightComponent.MaxBulbs);
				if (EditorGUI.EndChangeCheck()) {
					Undo.RecordObject(material, "Change Insert Lamp Count");
					component.SetBulbCount(wanted);
					EditorUtility.SetDirty(material);
					SceneView.RepaintAll();
					count = component.BulbCount;
				}
			}
			using (new EditorGUILayout.HorizontalScope()) {
				GUILayout.Label("#", GUILayout.Width(18f));
				GUILayout.Label(new GUIContent("Name", "Name shown in Lamp Manager when this insert has several lamps."), GUILayout.Width(65f));
				GUILayout.Label("Color", GUILayout.Width(38f));
				GUILayout.Label(new GUIContent("Position (mm)", "Lamp position in insert-local X, Y and Z. Drag its handle in Scene view to move it."));
			}
			for (var i = 0; i < count; i++) {
				var extra = i > 0 ? _extraBulbs.GetArrayElementAtIndex(i - 1) : null;
				var color = i == 0 ? _color : extra.FindPropertyRelative(nameof(InsertLightComponent.Bulb.Color));
				using (new EditorGUILayout.HorizontalScope()) {
					GUILayout.Label((i + 1).ToString(), GUILayout.Width(18f));
					EditorGUILayout.PropertyField(_bulbNames.GetArrayElementAtIndex(i), GUIContent.none, GUILayout.Width(65f));
					EditorGUILayout.PropertyField(color, GUIContent.none, GUILayout.Width(38f));
					using (new EditorGUI.DisabledScope(Application.isPlaying || readOnly)) {
						EditorGUI.BeginChangeCheck();
						var position = EditorGUILayout.Vector3Field(GUIContent.none, component.BulbPosition(i), GUILayout.MinWidth(120f));
						if (EditorGUI.EndChangeCheck()) {
							Undo.RecordObject(material, "Move Insert Lamp");
							component.SetBulbPosition(i, position);
							EditorUtility.SetDirty(material);
							SceneView.RepaintAll();
						}
					}
				}
			}
			if (readOnly) {
				EditorGUILayout.HelpBox("This insert material is read-only. Fork the insert to the project to change its lamp count or positions.", MessageType.Info);
			}
		}

		private void OnSceneGUI()
		{
			if (Application.isPlaying || Selection.count != 1) {
				return;
			}
			var component = (InsertLightComponent)target;
			var material = component.GetComponent<MeshRenderer>().sharedMaterial;
			if (material == null || !material.HasProperty(LuminosityId)) {
				return;
			}
			var readOnly = MaterialIsReadOnly(material);
			var originalColor = Handles.color;
			var count = component.BulbCount;
			var transform = component.transform;
			var up = transform.up;
			var across = transform.right;
			var along = transform.forward;
			for (var i = 0; i < count; i++) {
				var position = component.BulbPosition(i);
				var world = transform.TransformPoint(position * 0.001f);
				var size = HandleUtility.GetHandleSize(world);
				// Hold the offset through a drag, so crossing the pivot cannot jump the lamp.
				if (GUIUtility.hotControl == 0) {
					_lampHandleOffsets[i] = Vector2.Distance(HandleUtility.WorldToGUIPoint(world), HandleUtility.WorldToGUIPoint(Tools.handlePosition)) < 20f
						? -(across + along) * size * 0.25f : Vector3.zero;
				}
				var handle = world + _lampHandleOffsets[i];
				Handles.color = BulbColors[i];
				if (handle != world) {
					Handles.DrawDottedLine(world, handle, 3f);
				}
				Handles.Label(handle + across * size * 0.08f, component.BulbLabel(i), LampLabel);
				if (readOnly) {
					Handles.CircleHandleCap(0, handle, Quaternion.LookRotation(up), size * 0.06f, EventType.Repaint);
					continue;
				}
				EditorGUI.BeginChangeCheck();
				var moved = Handles.Slider2D(handle, up, across, along, size * 0.06f, Handles.CircleHandleCap, Vector2.zero);
				if (!EditorGUI.EndChangeCheck()) {
					continue;
				}
				Undo.RecordObject(material, "Move Insert Lamp");
				var local = transform.InverseTransformPoint(world + moved - handle) * 1000f;
				component.SetBulbPosition(i, new Vector3(local.x, position.y, local.z));
				EditorUtility.SetDirty(material);
			}
			Handles.color = originalColor;
		}

		private static void Field(SerializedProperty property, string label)
		{
			EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
		}
	}
}
