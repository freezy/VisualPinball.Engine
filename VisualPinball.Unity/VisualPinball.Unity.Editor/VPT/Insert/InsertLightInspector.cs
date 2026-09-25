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
		private SerializedProperty _tintWithLamp;
		private SerializedProperty _extraBulbs;
		private SerializedProperty _lampTint;
		private SerializedProperty _spillLumens;
		private SerializedProperty _spillRange;
		private SerializedProperty _spillLight;

		private static readonly int LuminosityId = Shader.PropertyToID("_InsertLuminosity");

		private const string LampKey = "VisualPinball.InsertLight.Lamp";
		private const string SpillKey = "VisualPinball.InsertLight.Spill";

		private void OnEnable()
		{
			_intensity = serializedObject.FindProperty(nameof(InsertLightComponent.Intensity));
			_color = serializedObject.FindProperty(nameof(InsertLightComponent.Color));
			_tintWithLamp = serializedObject.FindProperty(nameof(InsertLightComponent.TintWithLamp));
			_extraBulbs = serializedObject.FindProperty(nameof(InsertLightComponent.ExtraBulbs));
			_lampTint = serializedObject.FindProperty(nameof(InsertLightComponent.LampTint));
			_spillLumens = serializedObject.FindProperty(nameof(InsertLightComponent.SpillLumens));
			_spillRange = serializedObject.FindProperty(nameof(InsertLightComponent.SpillRange));
			_spillLight = serializedObject.FindProperty(nameof(InsertLightComponent.SpillLight));
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			// Without the hybrid material there is nothing for the lamp to drive.
			var hybrid = targets.All(selected => {
				var material = ((InsertLightComponent)selected).GetComponent<MeshRenderer>().sharedMaterial;
				return material != null && material.HasProperty(LuminosityId);
			});
			if (!hybrid) {
				EditorGUILayout.HelpBox("The insert's material is not the hybrid material, so Intensity and Color have nothing to drive and the spill stays off. Bake the insert with Hybrid Optics and click Use HDRP Hybrid Material on it.", MessageType.Warning);
			}

			if (InsertSection.Begin(LampKey, "Lamp")) {
				using (new EditorGUI.DisabledScope(!hybrid)) {
					Field(_intensity, "Intensity");
					Field(_color, "Color");
					// One row per bulb the material has beyond the first.
					var bulbs = targets.Length == 1 ? ((InsertLightComponent)target).BulbCount - 1 : 0;
					for (var i = 0; i < bulbs && i < _extraBulbs.arraySize; i++) {
						var bulb = _extraBulbs.GetArrayElementAtIndex(i);
						Field(bulb.FindPropertyRelative(nameof(InsertLightComponent.Bulb.Intensity)), $"Bulb {i + 2} Intensity");
						Field(bulb.FindPropertyRelative(nameof(InsertLightComponent.Bulb.Color)), $"Bulb {i + 2} Color");
					}
					if (bulbs > 0) {
						EditorGUILayout.HelpBox($"Each bulb is a lamp of its own in Lamp Manager: the Element column offers Bulb 1 to Bulb {bulbs + 1} for this insert.", MessageType.None);
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
					var lumens = _spillLumens.floatValue * Mathf.Max(0f, material.GetFloat(LuminosityId)) * _intensity.floatValue;
					EditorGUILayout.LabelField(new GUIContent("Configured Spill", "Lumens times the material's Max Brightness times the Intensity above: what the light puts out at that intensity. While playing, the game's lamp level takes the place of Intensity."), new GUIContent($"{lumens:0.###} lm"));
				}
			}
			InsertSection.End();

			serializedObject.ApplyModifiedProperties();
		}

		private static void Field(SerializedProperty property, string label)
		{
			EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
		}
	}
}
