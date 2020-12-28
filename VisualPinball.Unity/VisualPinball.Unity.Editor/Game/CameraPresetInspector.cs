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
			EditorGUILayout.LabelField("Pivot Location", EditorStyles.boldLabel);
			_preset.offset.x = EditorGUILayout.Slider("Offset X", _preset.offset.x, -0.2f, 0.2f);
			_preset.offset.y = EditorGUILayout.Slider("Offset Y", _preset.offset.y, -1f, 1f);
			_preset.offset.z = EditorGUILayout.Slider("Offset Z", _preset.offset.z, -1f, 1f);

			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			EditorGUILayout.LabelField("View", EditorStyles.boldLabel);
			_preset.fov = EditorGUILayout.Slider("FOV", _preset.fov, 2f, 80f);
			_preset.distance = EditorGUILayout.Slider("Distance", _preset.distance, 0f, 10f);

			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			EditorGUILayout.LabelField("Orientation", EditorStyles.boldLabel);
			_preset.angle = EditorGUILayout.Slider("Angle", _preset.angle, 0f, 180f);
			_preset.orbit = EditorGUILayout.Slider("Orbit", _preset.orbit, 0f, 360f);

			EditorUtility.SetDirty(_preset);
		}
	}
}
