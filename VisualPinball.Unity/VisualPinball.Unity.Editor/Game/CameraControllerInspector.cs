// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(CameraController)), CanEditMultipleObjects]
	public class CameraControllerInspector : BaseEditor<CameraController>
	{
		private const string ActiveAssetPath = "Assets/EditorResources/Camera/_activeCameraSetting.asset";
		private const string PresetAssetPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/EditorResources/Camera";

		private CameraController _cameraController;
		private SerializedProperty _cameraPresetsProp; // used to render the preset list
		private int _presetIndex;
		private bool _activeDirty;

		private void OnEnable()
		{
			_cameraController = target as CameraController;
			_cameraPresetsProp = FindProperty( x => x.cameraPresets);

			Initialize();
		}
		private void Initialize()
		{
			// 1. load default presets
			if (_cameraController.cameraPresets == null || _cameraController.cameraPresets.Count == 0) {

				_cameraController.cameraPresets = new List<CameraSetting> {
					AssetDatabase.LoadAssetAtPath<CameraSetting>($"{PresetAssetPath}/Standard Flat.asset"),
					AssetDatabase.LoadAssetAtPath<CameraSetting>($"{PresetAssetPath}/Top Down.asset"),
					AssetDatabase.LoadAssetAtPath<CameraSetting>($"{PresetAssetPath}/Wide.asset")
				};
			}

			// 2. set active setting
			if (_cameraController.activeSetting == null) {

				if (File.Exists(ActiveAssetPath)) {
					_cameraController.activeSetting = AssetDatabase.LoadAssetAtPath<CameraSetting>(ActiveAssetPath);

				} else {
					_cameraController.activeSetting = _cameraController.cameraPresets[0].Clone();
					SaveActiveSetting();
				}
			}
		}

		public override void OnInspectorGUI()
		{
			// handle invalid hierarchy
			if (!_cameraController.Camera) {
				EditorGUILayout.HelpBox("No camera found! Note that you shouldn't apply this Component manually, it's part of a prefab provided by VPE.", MessageType.Error);
				return;
			}

			// preset slider
			if (_cameraController.cameraPresets.Count > 0) {
				var currentIndex = _presetIndex;

				var dirtySuffix = _activeDirty ? "*" : "";
				EditorGUILayout.LabelField(_cameraController.activeSetting.DisplayName + dirtySuffix, EditorStyles.boldLabel);
				_presetIndex = EditorGUILayout.IntSlider("Active Preset", _presetIndex, 0, _cameraController.cameraPresets.Count - 1);
				if (currentIndex != _presetIndex) {
					ApplySetting();
				}
			}

			// sliders for the active setting
			EditorGUILayout.Space();
			EditorGUILayout.Separator();
			EditorGUI.BeginChangeCheck();
			CameraSettingInspector.Gui(_cameraController.activeSetting);
			if (EditorGUI.EndChangeCheck()) {
				_activeDirty = true;
				_cameraController.ApplySetting();
			}

			var noPresets = _cameraController.cameraPresets.Count == 0;
			var selectedNullPreset = !noPresets && _cameraController.cameraPresets[_presetIndex] == null;

			// buttons
			EditorGUILayout.Space();
			EditorGUILayout.Separator();
			EditorGUILayout.BeginHorizontal();
			var dir = Path.GetDirectoryName(ActiveAssetPath);
			EditorGUI.BeginDisabledGroup(selectedNullPreset);
			if (GUILayout.Button("Clone")) {
				var path = EditorUtility.SaveFilePanelInProject("Save camera setting as preset",
					$"{_cameraController.activeSetting.DisplayName}.asset", "asset", "Save new camera setting as preset", dir);
				CloneSetting(path);
			}
			EditorGUI.EndDisabledGroup();

			EditorGUI.BeginDisabledGroup(selectedNullPreset || noPresets || IsPackageAsset(_cameraController.cameraPresets[_presetIndex]));
			if (GUILayout.Button("Save")) {
				SaveSetting();
			}

			if (GUILayout.Button("Delete")) {
				DeleteSetting();
			}

			EditorGUI.EndDisabledGroup();

			EditorGUI.BeginDisabledGroup(!_activeDirty || selectedNullPreset || noPresets);
			if (GUILayout.Button("Reset")) {
				ApplySetting();
			}
			EditorGUI.EndDisabledGroup();

			EditorGUILayout.EndHorizontal();

			// saved presets
			EditorGUILayout.Space();
			EditorGUILayout.Separator();
			EditorGUILayout.PropertyField(_cameraPresetsProp);

			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			//Editor Play Mode Camera Controls 
			EditorGUILayout.LabelField("Runtime Motion Controls", EditorStyles.boldLabel);
			EditorGUILayout.Space();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Horizontal Speed", EditorStyles.boldLabel);
			_cameraController.mouseSpeedH = EditorGUILayout.Slider("", _cameraController.mouseSpeedH, 0f, 5f);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Vertical Speed", EditorStyles.boldLabel);
			_cameraController.mouseSpeedV = EditorGUILayout.Slider("", _cameraController.mouseSpeedV, 0f, 5f);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Translation Speed", EditorStyles.boldLabel);
			_cameraController.mouseSpeedT = EditorGUILayout.Slider("", _cameraController.mouseSpeedT, 0f, 5f);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			/*
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Distance Change Multiplier", EditorStyles.boldLabel);
			_cameraController.mouseSpeedD = EditorGUILayout.Slider("", _cameraController.mouseSpeedD, 0f, 2f);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("FOV Change Multiplier", EditorStyles.boldLabel);
			_cameraController.mouseSpeedZ = EditorGUILayout.Slider("", _cameraController.mouseSpeedZ, 0f, 2f);
			EditorGUILayout.EndHorizontal();
			*/

			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			EditorGUILayout.BeginHorizontal(); 
			EditorGUILayout.LabelField("Invert Horizontal Axis", EditorStyles.boldLabel);
			_cameraController.invertX = EditorGUILayout.Toggle(_cameraController.invertX);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Invert Vertical Axis", EditorStyles.boldLabel);
			_cameraController.invertY = EditorGUILayout.Toggle(_cameraController.invertY);
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Use Inertia", EditorStyles.boldLabel);
			_cameraController.useInertia = EditorGUILayout.Toggle(_cameraController.useInertia);
			EditorGUILayout.EndHorizontal();



		}

		private void ApplySetting()
		{
			if (_presetIndex < 0 || _cameraController.cameraPresets.Count == 0) {
				return;
			}
			_cameraController.activeSetting.ApplyFrom(_cameraController.cameraPresets[_presetIndex]);
			_cameraController.ApplySetting();
			_activeDirty = false;
		}

		private void CloneSetting(string path)
		{
			if (string.IsNullOrEmpty(path)) {
				return;
			}

			// set name and clone
			_cameraController.activeSetting.name = Path.GetFileNameWithoutExtension(path);
			_cameraController.activeSetting.displayName = _cameraController.activeSetting.name;
			var setting = _cameraController.activeSetting.Clone();

			// save
			AssetDatabase.CreateAsset(setting, path);
			AssetDatabase.SaveAssets();

			// add to list and select
			_cameraController.cameraPresets.Add(setting);
			_presetIndex = _cameraController.cameraPresets.Count - 1;
			_activeDirty = false;
		}

		private void SaveSetting()
		{
			_cameraController.cameraPresets[_presetIndex].ApplyFrom(_cameraController.activeSetting);
			_activeDirty = false;
		}

		private void DeleteSetting()
		{
			if (_cameraController.cameraPresets.Count == 0) {
				return;
			}

			// delete asset
			var setting = _cameraController.cameraPresets[_presetIndex];
			AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(setting));

			// remove from list
			_cameraController.cameraPresets.RemoveAt(_presetIndex);
			_presetIndex = math.clamp(_presetIndex, 0, _cameraController.cameraPresets.Count - 1);

			// apply next preset
			ApplySetting();
		}

		private void SaveActiveSetting()
		{
			var dir = Path.GetDirectoryName(ActiveAssetPath);
			if (!Directory.Exists(dir)) {
				Directory.CreateDirectory(dir);
			}
			AssetDatabase.CreateAsset(_cameraController.activeSetting, ActiveAssetPath);
			AssetDatabase.SaveAssets();
		}

		private static bool IsPackageAsset(Object preset)
		{
			return AssetDatabase.GetAssetPath(preset).StartsWith(PresetAssetPath);
		}
	}
}
