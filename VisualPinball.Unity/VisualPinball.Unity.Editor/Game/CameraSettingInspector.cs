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

using UnityEditor;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(CameraSetting)), CanEditMultipleObjects]
	public class CameraSettingInspector : UnityEditor.Editor
	{
		private CameraSetting _setting;

		private void OnEnable()
		{
			_setting = target as CameraSetting;
		}

		public override void OnInspectorGUI()
		{
			Gui(_setting);
		}

		public static void Gui(CameraSetting setting)
		{
			EditorGUI.BeginChangeCheck();

			EditorGUILayout.LabelField("Pivot Location", EditorStyles.boldLabel);
			setting.offset.x = EditorGUILayout.Slider("Offset X", setting.offset.x, -0.2f, 0.2f);
			setting.offset.y = EditorGUILayout.Slider("Offset Y", setting.offset.y, -1f, 1f);
			setting.offset.z = EditorGUILayout.Slider("Offset Z", setting.offset.z, -1f, 1f);

			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			EditorGUILayout.LabelField("View", EditorStyles.boldLabel);
			setting.fov = EditorGUILayout.Slider("FOV", setting.fov, 2f, 80f);
			setting.distance = EditorGUILayout.Slider("Distance", setting.distance, 0f, 10f);

			EditorGUILayout.Space();
			EditorGUILayout.Separator();

			EditorGUILayout.LabelField("Orientation", EditorStyles.boldLabel);
			setting.angle = EditorGUILayout.Slider("Angle", setting.angle, 0f, 180f);
			setting.orbit = EditorGUILayout.Slider("Orbit", setting.orbit, 0f, 360f);

			if (EditorGUI.EndChangeCheck()) {
				EditorUtility.SetDirty(setting);
			}
		}


	}
}
