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

using System;
using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(CameraController)), CanEditMultipleObjects]
	public class CameraControllerInspector : UnityEditor.Editor
	{
		private CameraController _cameraController;
		private SerializedProperty _cameraPresetsProp;

		private bool _subscribed;

		private void OnEnable()
		{
			_cameraController = target as CameraController;
			_cameraPresetsProp = serializedObject.FindProperty("cameraPresets");
		}

		public override void OnInspectorGUI()
		{
			if (!_cameraController.Camera) {
				EditorGUILayout.HelpBox("No camera found! Note that you shouldn't apply this Component manually, it's part of a prefab provided by VPE.", MessageType.Error);
				return;
			}

			if (_cameraController.cameraPresets.Length > 0) {
				var currentIndex = _cameraController.presetIndex;
				_cameraController.presetIndex = EditorGUILayout.IntSlider("Active Preset", _cameraController.presetIndex, 0, _cameraController.cameraPresets.Length - 1);
				if (currentIndex != _cameraController.presetIndex) {
					_cameraController.ApplyPreset();
					if (_subscribed) {
						_cameraController.cameraPresets[currentIndex].OnPresetUpdated -= ApplyPreset;
					}
					_cameraController.cameraPresets[_cameraController.presetIndex].OnPresetUpdated += ApplyPreset;
					_subscribed = true;
				}
			}

			EditorGUILayout.Space();
			EditorGUILayout.Separator();
			EditorGUILayout.PropertyField(_cameraPresetsProp);
		}

		private void ApplyPreset(object sender, EventArgs e) => _cameraController.ApplyPreset();
	}

}
