﻿using UnityEditor;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Editor.DragPoint;
using VisualPinball.Unity.VPT.Trigger;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(TriggerBehavior))]
	public class TriggerInspector : DragPointsItemInspector
	{
		private TriggerBehavior _trigger;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		private static string[] _triggerShapeStrings = {
			"TriggerNone",
			"TriggerWireA",
			"TriggerStar",
			"TriggerWireB",
			"TriggerButton",
			"TriggerWireC",
			"TriggerWireD",
		};
		private static int[] _triggerShapeValues = {
			TriggerShape.TriggerNone,
			TriggerShape.TriggerWireA,
			TriggerShape.TriggerStar,
			TriggerShape.TriggerWireB,
			TriggerShape.TriggerButton,
			TriggerShape.TriggerWireC,
			TriggerShape.TriggerWireD,
		};

		protected override void OnEnable()
		{
			base.OnEnable();
			_trigger = target as TriggerBehavior;
		}

		public override void OnInspectorGUI()
		{
			base.OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				ItemDataField("Visible", ref _trigger.data.IsVisible);
				DropDownField("Shape", ref _trigger.data.Shape, _triggerShapeStrings, _triggerShapeValues);
				ItemDataField("Wire Thickness", ref _trigger.data.WireThickness);
				ItemDataField("Star Radius", ref _trigger.data.Radius);
				ItemDataField("Rotation", ref _trigger.data.Rotation);
				ItemDataField("Animation Speed", ref _trigger.data.AnimSpeed, dirtyMesh: false);
				MaterialField("Material", ref _trigger.data.Material);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _trigger.data.Center);
				SurfaceField("Surface", ref _trigger.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "State & Physics")) {
				ItemDataField("Enabled", ref _trigger.data.IsEnabled, dirtyMesh: false);
				ItemDataField("Hit Height", ref _trigger.data.HitHeight, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _trigger.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _trigger.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
