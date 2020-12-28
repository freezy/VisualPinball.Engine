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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(CameraController)), CanEditMultipleObjects]
	public class CameraControllerInspector : UnityEditor.Editor
	{
		private CameraController _cameraController;

		private void OnEnable()
		{
			_cameraController = target as CameraController;
		}

		public override void OnInspectorGUI()
		{
			if (!_cameraController.Camera) {
				EditorGUILayout.HelpBox("Camera controller must sit on GameObject with a camera.", MessageType.Error);
				return;
			}

			_cameraController.cameraPreset = (CameraPreset)EditorGUILayout.ObjectField("Camera Preset", _cameraController.cameraPreset, typeof(CameraPreset), false);
			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			EditorGUI.BeginDisabledGroup(!TableSelector.Instance.HasSelectedTable || _cameraController.cameraPreset == null);
			if (GUILayout.Button("Apply Preset")) {
				_cameraController.ApplyPreset();
			}
			EditorGUI.EndDisabledGroup();
		}
	}

}
