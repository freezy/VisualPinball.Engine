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

// ReSharper disable AssignmentInConditionalExpression

using UnityEditor;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Light;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(LightAuthoring))]
	public class LightInspector : ItemMainInspector<Light, LightData, LightAuthoring>
	{
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutState = true;
		private bool _foldoutMisc;

		private static readonly string[] LightStateLabels = { "Off", "On", "Blinking" };
		private static readonly int[] LightStateValues = { LightStatus.LightStateOff, LightStatus.LightStateOn, LightStatus.LightStateBlinking };

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			ItemDataField("Position", ref Data.Center);
			SurfaceField("Surface", ref Data.Surface);

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				ItemDataField("Falloff", ref Data.Falloff, false);
				ItemDataField("Intensity", ref Data.Intensity, false);

				EditorGUILayout.LabelField("Fade Speed");
				EditorGUI.indentLevel++;
				ItemDataField("Up", ref Data.FadeSpeedUp, false);
				ItemDataField("Down", ref Data.FadeSpeedDown, false);
				EditorGUI.indentLevel--;

				ItemDataField("Color", ref Data.Color2, false); // Note: using color2 since that's the hot/center color in vpx

				EditorGUILayout.LabelField("Bulb");
				EditorGUI.indentLevel++;
				ItemDataField("Enable", ref Data.ShowBulbMesh, false, onChanged: ItemAuthoring.OnBulbEnabled);
				ItemDataField("Scale Mesh", ref Data.MeshRadius, false);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();


			if (_foldoutState = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutState, "State")) {
				DropDownField("State", ref Data.State, LightStateLabels, LightStateValues);
				ItemDataField("Blink Pattern", ref Data.BlinkPattern, false);
				ItemDataField("Blink Interval", ref Data.BlinkInterval, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref Data.IsTimerEnabled, false);
				ItemDataField("Timer Interval", ref Data.TimerInterval, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
