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

using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(CameraController)), CanEditMultipleObjects]
	public class CameraControllerInspector : UnityEditor.Editor
	{
		private const string ActiveAssetPath = "Assets/EditorResources/Camera/_activeCameraPreset.asset";
		private const string PresetAssetPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/EditorResources/Camera";

		private CameraController _cameraController;
		private SerializedProperty _cameraPresetsProp; // used to render the preset list
		private int _presetIndex;
		private bool _activeDirty;

		private void OnEnable()
		{
			_cameraController = target as CameraController;
			_cameraPresetsProp = serializedObject.FindProperty("cameraPresets");
			Initialize();
		}
		private void Initialize()
		{
			// 1. load default presets
			if (_cameraController.cameraPresets == null || _cameraController.cameraPresets.Count == 0) {

				_cameraController.cameraPresets = new List<CameraPreset> {
					AssetDatabase.LoadAssetAtPath<CameraPreset>($"{PresetAssetPath}/Standard Flat.asset"),
					AssetDatabase.LoadAssetAtPath<CameraPreset>($"{PresetAssetPath}/Top Down.asset"),
					AssetDatabase.LoadAssetAtPath<CameraPreset>($"{PresetAssetPath}/Wide.asset")
				};
			}

			// 2. set active preset
			if (_cameraController.activePreset == null) {

				if (File.Exists(ActiveAssetPath)) {
					_cameraController.activePreset = AssetDatabase.LoadAssetAtPath<CameraPreset>(ActiveAssetPath);

				} else {
					_cameraController.activePreset = _cameraController.cameraPresets[0].Clone();
					SaveActivePreset();
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
				EditorGUILayout.LabelField(_cameraController.activePreset.DisplayName + dirtySuffix, EditorStyles.boldLabel);
				_presetIndex = EditorGUILayout.IntSlider("Active Preset", _presetIndex, 0, _cameraController.cameraPresets.Count - 1);
				if (currentIndex != _presetIndex) {
					ApplyPreset();
				}
			}

			// sliders for the active preset
			EditorGUILayout.Space();
			EditorGUILayout.Separator();
			EditorGUI.BeginChangeCheck();
			CameraPresetInspector.Gui(_cameraController.activePreset);
			if (EditorGUI.EndChangeCheck()) {
				_activeDirty = true;
				_cameraController.ApplyPreset();
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
				var path = EditorUtility.SaveFilePanelInProject("Save camera preset",
					$"{_cameraController.activePreset.DisplayName}.asset", "asset", "Save new camera preset", dir);
				ClonePreset(path);
			}
			EditorGUI.EndDisabledGroup();

			EditorGUI.BeginDisabledGroup(selectedNullPreset || noPresets || IsPackageAsset(_cameraController.cameraPresets[_presetIndex]));
			if (GUILayout.Button("Save")) {
				SavePreset();
			}

			if (GUILayout.Button("Delete")) {
				DeletePreset();
			}

			EditorGUI.EndDisabledGroup();

			EditorGUI.BeginDisabledGroup(!_activeDirty || selectedNullPreset || noPresets);
			if (GUILayout.Button("Reset")) {
				ApplyPreset();
			}
			EditorGUI.EndDisabledGroup();

			EditorGUILayout.EndHorizontal();

			// saved presets
			EditorGUILayout.Space();
			EditorGUILayout.Separator();
			EditorGUILayout.PropertyField(_cameraPresetsProp);
		}

		private void ApplyPreset()
		{
			_cameraController.activePreset.ApplyFrom(_cameraController.cameraPresets[_presetIndex]);
			_cameraController.ApplyPreset();
			_activeDirty = false;
		}

		private void ClonePreset(string path)
		{
			if (string.IsNullOrEmpty(path)) {
				return;
			}

			// set name and clone
			_cameraController.activePreset.name = Path.GetFileNameWithoutExtension(path);
			_cameraController.activePreset.displayName = _cameraController.activePreset.name;
			var preset = _cameraController.activePreset.Clone();

			// save
			AssetDatabase.CreateAsset(preset, path);
			AssetDatabase.SaveAssets();

			// add to list and select
			_cameraController.cameraPresets.Add(preset);
			_presetIndex = _cameraController.cameraPresets.Count - 1;
			_activeDirty = false;
		}

		private void SavePreset()
		{
			_cameraController.cameraPresets[_presetIndex].ApplyFrom(_cameraController.activePreset);
			_activeDirty = false;
		}

		private void DeletePreset()
		{
			if (_cameraController.cameraPresets.Count == 0) {
				return;
			}

			// delete asset
			var preset = _cameraController.cameraPresets[_presetIndex];
			File.Delete(AssetDatabase.GetAssetPath(preset));

			// remove from list
			_cameraController.cameraPresets.RemoveAt(_presetIndex);
			_presetIndex = math.clamp(_presetIndex, 0, _cameraController.cameraPresets.Count - 1);

			// apply next preset
			ApplyPreset();
		}

		private void SaveActivePreset()
		{
			var dir = Path.GetDirectoryName(ActiveAssetPath);
			if (!Directory.Exists(dir)) {
				Directory.CreateDirectory(dir);
			}
			AssetDatabase.CreateAsset(_cameraController.activePreset, ActiveAssetPath);
			AssetDatabase.SaveAssets();
		}

		private bool IsPackageAsset(Object preset)
		{
			return AssetDatabase.GetAssetPath(preset).StartsWith(PresetAssetPath);
		}

	}

}
