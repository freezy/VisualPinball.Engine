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
	[CustomEditor(typeof(TriggerAuthoring))]
	public class TriggerInspector : DragPointsItemInspector
	{
		private TriggerAuthoring _trigger;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		private static string[] _triggerShapeStrings = {
			"None",
			"Button",
			"Star",
			"Wire A",
			"Wire B",
			"Wire C",
			"Wire D",
		};
		private static int[] _triggerShapeValues = {
			TriggerShape.TriggerNone,
			TriggerShape.TriggerButton,
			TriggerShape.TriggerStar,
			TriggerShape.TriggerWireA,
			TriggerShape.TriggerWireB,
			TriggerShape.TriggerWireC,
			TriggerShape.TriggerWireD,
		};

		protected override void OnEnable()
		{
			base.OnEnable();
			_trigger = target as TriggerAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				ItemDataField("Visible", ref _trigger.Data.IsVisible);
				DropDownField("Shape", ref _trigger.Data.Shape, _triggerShapeStrings, _triggerShapeValues);
				ItemDataField("Wire Thickness", ref _trigger.Data.WireThickness);
				ItemDataField("Star Radius", ref _trigger.Data.Radius);
				ItemDataField("Rotation", ref _trigger.Data.Rotation);
				ItemDataField("Animation Speed", ref _trigger.Data.AnimSpeed, dirtyMesh: false);
				MaterialField("Material", ref _trigger.Data.Material);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _trigger.Data.Center);
				SurfaceField("Surface", ref _trigger.Data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "State & Physics")) {
				ItemDataField("Enabled", ref _trigger.Data.IsEnabled, dirtyMesh: false);
				ItemDataField("Hit Height", ref _trigger.Data.HitHeight, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _trigger.Data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _trigger.Data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
