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
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(TriggerAuthoring))]
	public class TriggerInspector : DragPointsItemInspector<Trigger, TriggerData, TriggerAuthoring>
	{
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		private static readonly string[] TriggerShapeLabels = {
			"None",
			"Button",
			"Star",
			"Wire A",
			"Wire B",
			"Wire C",
			"Wire D",
		};
		private static readonly int[] TriggerShapeValues = {
			TriggerShape.TriggerNone,
			TriggerShape.TriggerButton,
			TriggerShape.TriggerStar,
			TriggerShape.TriggerWireA,
			TriggerShape.TriggerWireB,
			TriggerShape.TriggerWireC,
			TriggerShape.TriggerWireD,
		};

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				ItemDataField("Visible", ref Data.IsVisible);
				DropDownField("Shape", ref Data.Shape, TriggerShapeLabels, TriggerShapeValues);
				ItemDataField("Wire Thickness", ref Data.WireThickness);
				ItemDataField("Star Radius", ref Data.Radius);
				ItemDataField("Rotation", ref Data.Rotation);
				ItemDataField("Animation Speed", ref Data.AnimSpeed, false);
				MaterialField("Material", ref Data.Material);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref Data.Center);
				SurfaceField("Surface", ref Data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "State & Physics")) {
				ItemDataField("Enabled", ref Data.IsEnabled, false);
				ItemDataField("Hit Height", ref Data.HitHeight, false);
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
