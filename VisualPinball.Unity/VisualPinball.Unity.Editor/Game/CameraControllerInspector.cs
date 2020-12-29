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

using System.IO;
using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(CameraController)), CanEditMultipleObjects]
	public class CameraControllerInspector : UnityEditor.Editor
	{
		private CameraController _cameraController;

		private SerializedProperty _cameraPresetsProp;
		private SerializedProperty _cameraActivePresetProp;

		private const string ActiveAssetPath = "Assets/EditorResources/Camera/_activeCameraPreset.asset";
		private bool _subscribed;

		private void OnEnable()
		{
			_cameraController = target as CameraController;

			Initialize();

			_cameraActivePresetProp = serializedObject.FindProperty("activePreset");
			_cameraPresetsProp = serializedObject.FindProperty("cameraPresets");
		}

		private void Initialize()
		{
			// 1. load default presets
			if (_cameraController.cameraPresets == null || _cameraController.cameraPresets.Length == 0) {


				const string presetAssetPath = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/EditorResources/Camera";
				_cameraController.cameraPresets = new[] {
					AssetDatabase.LoadAssetAtPath<CameraPreset>($"{presetAssetPath}/Standard Flat.asset"),
					AssetDatabase.LoadAssetAtPath<CameraPreset>($"{presetAssetPath}/Top Down.asset"),
					AssetDatabase.LoadAssetAtPath<CameraPreset>($"{presetAssetPath}/Wide.asset")
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

			EditorGUI.BeginChangeCheck();

			// sliders for the active preset
			CameraPresetInspector.Gui(_cameraController.activePreset);

			if (_cameraController.cameraPresets.Length > 0) {
				var currentIndex = _cameraController.presetIndex;

				EditorGUILayout.LabelField(_cameraController.activePreset.name, EditorStyles.boldLabel);
				_cameraController.presetIndex = EditorGUILayout.IntSlider("Active Preset", _cameraController.presetIndex, 0, _cameraController.cameraPresets.Length - 1);
				if (currentIndex != _cameraController.presetIndex) {
					_cameraController.activePreset.ApplyFrom(_cameraController.cameraPresets[_cameraController.presetIndex]);
					_cameraController.ApplyPreset();
				}
			}

			if (EditorGUI.EndChangeCheck()) {
				_cameraController.ApplyPreset();
			}

			EditorGUILayout.Space();
			EditorGUILayout.Separator();
			EditorGUILayout.PropertyField(_cameraPresetsProp);
		}

		private void SaveActivePreset()
		{
			AssetDatabase.CreateAsset(_cameraController.activePreset, ActiveAssetPath);
			AssetDatabase.SaveAssets();
		}
	}

}
