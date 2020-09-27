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
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(LightAuthoring))]
	public class LightInspector : ItemInspector
	{
		private LightAuthoring _light;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutStateAndPhysics = true;
		private bool _foldoutMisc = true;

		private static string[] _lightStateStrings = { "Off", "On", "Blinking" };
		private static int[] _lightStateValues = { LightStatus.LightStateOff, LightStatus.LightStateOn, LightStatus.LightStateBlinking };

		protected override void OnEnable()
		{
			base.OnEnable();
			_light = target as LightAuthoring;
		}

		public override void OnInspectorGUI()
		{
			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				ItemDataField("Falloff", ref _light.Data.Falloff, dirtyMesh: false);
				ItemDataField("Intensity", ref _light.Data.Intensity, dirtyMesh: false);

				EditorGUILayout.LabelField("Fade Speed");
				EditorGUI.indentLevel++;
				ItemDataField("Up", ref _light.Data.FadeSpeedUp, dirtyMesh: false);
				ItemDataField("Down", ref _light.Data.FadeSpeedDown, dirtyMesh: false);
				EditorGUI.indentLevel--;

				ItemDataField("Color", ref _light.Data.Color2, dirtyMesh: false); // Note: using color2 since that's the hot/center color in vpx

				EditorGUILayout.LabelField("Bulb");
				EditorGUI.indentLevel++;
				ItemDataField("Enable", ref _light.Data.IsBulbLight, dirtyMesh: false);
				ItemDataField("Scale Mesh", ref _light.Data.MeshRadius, dirtyMesh: false);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _light.Data.Center);
				SurfaceField("Surface", ref _light.Data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutStateAndPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutStateAndPhysics, "State & Physics")) {
				DropDownField("State", ref _light.Data.State, _lightStateStrings, _lightStateValues);
				ItemDataField("Blink Pattern", ref _light.Data.BlinkPattern, dirtyMesh: false);
				ItemDataField("Blink Interval", ref _light.Data.BlinkInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _light.Data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _light.Data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
