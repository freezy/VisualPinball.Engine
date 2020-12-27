// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace VisualPinball.Unity.Editor.Player
{
	[CustomEditor(typeof(CameraController)), CanEditMultipleObjects]
	public class CameraControllerInspectorCameraControllerEditor : UnityEditor.Editor
	{

		SerializedProperty preset;
		SerializedProperty xoffset;
		SerializedProperty yoffset;
		SerializedProperty zoffset;
		SerializedProperty fov;
		SerializedProperty distance;
		SerializedProperty angle;
		SerializedProperty orbit;
		SerializedProperty presetName;
		CameraController cameraController;

		private void OnEnable()
		{

			preset = serializedObject.FindProperty("Preset");
			xoffset = serializedObject.FindProperty("XOffset");
			yoffset = serializedObject.FindProperty("YOffset");
			zoffset = serializedObject.FindProperty("ZOffset");
			fov = serializedObject.FindProperty("FOV");
			distance = serializedObject.FindProperty("Distance");
			angle = serializedObject.FindProperty("Angle");
			orbit = serializedObject.FindProperty("Orbit");
			presetName = serializedObject.FindProperty("PresetName");
		}


		public override void OnInspectorGUI()
		{
			//base.OnInspectorGUI();
			//base.OnInspectorGUI();
			cameraController = target as CameraController;
			serializedObject.Update();

			EditorGUILayout.LabelField("Editor View Camera Controller");
			EditorGUILayout.Separator();

			EditorGUILayout.LabelField("Pivot Location");
			EditorGUILayout.PropertyField(xoffset);
			EditorGUILayout.PropertyField(yoffset);
			EditorGUILayout.PropertyField(zoffset);

			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			EditorGUILayout.LabelField("View");
			EditorGUILayout.PropertyField(fov);
			EditorGUILayout.PropertyField(distance);

			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			EditorGUILayout.LabelField("Orientation");
			EditorGUILayout.PropertyField(angle);
			EditorGUILayout.PropertyField(orbit);


			if(cameraController == null) return;

			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			var rect = EditorGUILayout.BeginHorizontal();
			Handles.color = Color.gray;
			Handles.DrawLine(new Vector2(rect.x - 15, rect.y), new Vector2(rect.width + 15, rect.y));
			EditorGUILayout.EndHorizontal();
			EditorGUILayout.Separator();

			EditorGUILayout.LabelField("Camera Presets");

			EditorGUILayout.PropertyField(presetName);
			EditorGUILayout.IntSlider(preset, 1, cameraController.presetCount);
			EditorGUILayout.Separator();

			serializedObject.ApplyModifiedProperties();

			rect = EditorGUILayout.BeginHorizontal();
			if(GUILayout.Button("Add New Preset"))
			{
				CreatePreset();
			}
			if(GUILayout.Button("Update Preset"))
			{
				SavePreset();
			}
			EditorGUILayout.EndHorizontal();

			if(GUILayout.Button("Delete Preset"))
			{
				RemovePreset();
			}

			if(GUILayout.Button("Load Settings"))
			{
				LoadSettings();
			}
			if(GUILayout.Button("Save Settings"))
			{
				cameraController.SaveSettings();

			}

			cameraController.ApplyProperties();

		}


		private void GetPrefereces()
		{
			preset = EditorPrefs.GetInt("ccPreset");
		}

		public void CreatePreset()
		{
			AddPreset(presetName, new Vector3(xOffset, yOffset, zOffset), fov, distance, angle, orbit);
			presetCount = cameraPresets.Count;
			SaveSettingsToFile();
		}

		public void RemovePreset()
		{
			cameraPresets.RemoveAt(preset-1);
			_prevPreset = Mathf.Max(0, preset - 1);
			preset = _prevPreset;
			presetCount = cameraPresets.Count;
			SaveSettingsToFile();
		}


		public void SavePreset()
		{
			var camPres = new CameraPreset {
				name = presetName,
				offset = offset,
				fov = fov,
				distance = distance,
				angle = angle,
				orbit = orbit
			};
			var location = Mathf.Max(0, preset - 1);
			cameraPresets[location] = camPres;
			SaveSettingsToFile();
		}

			public void LoadSettings()
		{
			GetSavedSettings();
		}

		public void SaveSettings()
		{
			SaveSettingsToFile();
		}

		private void GetSavedSettings()
		{
			var settingsPath =  Path.Combine(Application.persistentDataPath, SettingsFilename);
			var presetsPath = Path.Combine(Application.persistentDataPath, PresetsFilename);

			if(!File.Exists(settingsPath) || !File.Exists(presetsPath))
			{
				SetupPresets();

			} else {

				var json = File.ReadAllText(settingsPath);
				var readSettings = new CameraSettings();
				EditorJsonUtility.FromJsonOverwrite(json, readSettings);
				if(readSettings.StoredPresets > 0)
				{

					cameraPresets.Clear();
					var campres = File.ReadAllText(presetsPath);
					CameraPreset[] pres = JsonHelper.FromJson<CameraPreset>(campres);
					cameraPresets = new List<CameraPreset>(pres);
					if(cameraPresets.Count <= 0)
					{
						SetupPresets();
						return;
					}

					preset = 1;
					_prevPreset = 1;
				}

				offset = readSettings.cOffset;
				xOffset = offset.x;
				yOffset = offset.y;
				zOffset = offset.z;
				fov = readSettings.cFOV;
				distance = readSettings.cDistance;
				angle = readSettings.cAngle;
				orbit = readSettings.cOrbit;
				preset = readSettings.cPreset;
				_prevPreset = preset;
				presetCount = cameraPresets.Count;

				Debug.Log("Loaded Camera Settings");
			}

		}

		private void SaveSettingsToFile()
		{
			var settingsPath = Path.Combine(Application.persistentDataPath, SettingsFilename);
			var presetsPath = Path.Combine(Application.persistentDataPath, PresetsFilename);

			var cs = new CameraSettings();
			cs.cName = presetName;
			cs.cOffset = offset;
			cs.cFOV = fov;
			cs.cDistance = distance;
			cs.cPreset = preset;
			cs.cAngle = angle;
			cs.cOrbit = orbit;
			cs.StoredPresets = cameraPresets.Count;


			string jsSave = EditorJsonUtility.ToJson(cs);
			File.WriteAllText(settingsPath, jsSave);

			var camPres = cameraPresets.ToArray();
			string cPresets = JsonHelper.ToJson(camPres);
			File.WriteAllText(presetsPath, cPresets);


			Debug.Log("Saved Camera Settings: " + settingsPath);

		}

	}

}
