﻿using UnityEditor;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.VPT.Kicker;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(KickerBehavior))]
	public class KickerInspector : ItemInspector
	{
		private KickerBehavior _kicker;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		private static string[] _kickerTypeStrings = {
			"KickerInvisible",
			"KickerHole",
			"KickerCup",
			"KickerHoleSimple",
			"KickerWilliams",
			"KickerGottlieb",
			"KickerCup2",
		};
		private static int[] _kickerTypeValues = {
			KickerType.KickerInvisible,
			KickerType.KickerHole,
			KickerType.KickerCup,
			KickerType.KickerHoleSimple,
			KickerType.KickerWilliams,
			KickerType.KickerGottlieb,
			KickerType.KickerCup2,
		};

		protected override void OnEnable()
		{
			base.OnEnable();
			_kicker = target as KickerBehavior;
		}

		public override void OnInspectorGUI()
		{
			base.OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				MaterialField("Material", ref _kicker.data.Material);
				DropDownField("Display", ref _kicker.data.KickerType, _kickerTypeStrings, _kickerTypeValues);
				ItemDataField("Radius", ref _kicker.data.Radius);
				ItemDataField("Orientation", ref _kicker.data.Orientation);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _kicker.data.Center);
				SurfaceField("Surface", ref _kicker.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "State & Physics")) {
				ItemDataField("Enabled", ref _kicker.data.IsEnabled, dirtyMesh: false);
				ItemDataField("Fall Through", ref _kicker.data.FallThrough, dirtyMesh: false);
				ItemDataField("Legacy", ref _kicker.data.LegacyMode, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _kicker.data.Scatter, dirtyMesh: false);
				ItemDataField("Hit Accuracy", ref _kicker.data.HitAccuracy, dirtyMesh: false);
				ItemDataField("Hit Height", ref _kicker.data.HitHeight, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _kicker.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _kicker.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
