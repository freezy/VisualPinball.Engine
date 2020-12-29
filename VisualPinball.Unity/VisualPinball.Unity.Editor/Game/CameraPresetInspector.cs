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
	[CustomEditor(typeof(CameraPreset)), CanEditMultipleObjects]
	public class CameraPresetInspector : UnityEditor.Editor
	{
		private CameraPreset _preset;

		private void OnEnable()
		{
			_preset = target as CameraPreset;
		}

		public override void OnInspectorGUI()
		{
			Gui(_preset);
		}

		public static void Gui(CameraPreset preset)
		{
			EditorGUI.BeginChangeCheck();

			EditorGUILayout.LabelField("Pivot Location", EditorStyles.boldLabel);
			preset.offset.x = EditorGUILayout.Slider("Offset X", preset.offset.x, -0.2f, 0.2f);
			preset.offset.y = EditorGUILayout.Slider("Offset Y", preset.offset.y, -1f, 1f);
			preset.offset.z = EditorGUILayout.Slider("Offset Z", preset.offset.z, -1f, 1f);

			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			EditorGUILayout.LabelField("View", EditorStyles.boldLabel);
			preset.fov = EditorGUILayout.Slider("FOV", preset.fov, 2f, 80f);
			preset.distance = EditorGUILayout.Slider("Distance", preset.distance, 0f, 10f);

			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			EditorGUILayout.LabelField("Orientation", EditorStyles.boldLabel);
			preset.angle = EditorGUILayout.Slider("Angle", preset.angle, 0f, 180f);
			preset.orbit = EditorGUILayout.Slider("Orbit", preset.orbit, 0f, 360f);

			if (EditorGUI.EndChangeCheck()) {
				EditorUtility.SetDirty(preset);
			}
		}


	}
}
