﻿using UnityEditor;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.VPT.HitTarget;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(HitTargetBehavior))]
	public class HitTargetInspector : ItemInspector
	{
		private HitTargetBehavior _target;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		private static string[] _targetTypeStrings = {
			"DropTargetBeveled",
			"DropTargetSimple",
			"HitTargetRound",
			"HitTargetRectangle",
			"HitFatTargetRectangle",
			"HitFatTargetSquare",
			"DropTargetFlatSimple",
			"HitFatTargetSlim",
			"HitTargetSlim",
		};
		private static int[] _targetTypeValues = {
			TargetType.DropTargetBeveled,
			TargetType.DropTargetSimple,
			TargetType.HitTargetRound,
			TargetType.HitTargetRectangle,
			TargetType.HitFatTargetRectangle,
			TargetType.HitFatTargetSquare,
			TargetType.DropTargetFlatSimple,
			TargetType.HitFatTargetSlim,
			TargetType.HitTargetSlim,
		};

		protected override void OnEnable()
		{
			base.OnEnable();
			_target = target as HitTargetBehavior;
		}

		public override void OnInspectorGUI()
		{
			base.OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DropDownField("Type", ref _target.data.TargetType, _targetTypeStrings, _targetTypeValues);
				TextureField("Image", ref _target.data.Image);
				MaterialField("Material", ref _target.data.Material);
				ItemDataField("Drop Speed", ref _target.data.DropSpeed, dirtyMesh: false);
				ItemDataField("Raise Delay", ref _target.data.RaiseDelay, dirtyMesh: false);
				ItemDataField("Depth Bias", ref _target.data.DepthBias, dirtyMesh: false);
				ItemDataField("Visible", ref _target.data.IsVisible);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position & Translation")) {
				EditorGUILayout.LabelField("Position");
				EditorGUI.indentLevel++;
				ItemDataField("", ref _target.data.Position);
				EditorGUI.indentLevel--;

				EditorGUILayout.LabelField("Scale");
				EditorGUI.indentLevel++;
				ItemDataField("", ref _target.data.Size);
				EditorGUI.indentLevel--;

				ItemDataField("Orientation", ref _target.data.RotZ);

			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Has Hit Event", ref _target.data.UseHitEvent, dirtyMesh: false);
				ItemDataField("Hit Threshold", ref _target.data.Threshold, dirtyMesh: false);

				EditorGUI.BeginDisabledGroup(_target.data.OverwritePhysics);
				MaterialField("Physics Material", ref _target.data.PhysicsMaterial, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Overwrite Material Settings", ref _target.data.OverwritePhysics, dirtyMesh: false);

				EditorGUI.BeginDisabledGroup(!_target.data.OverwritePhysics);
				ItemDataField("Elasticity", ref _target.data.Elasticity, dirtyMesh: false);
				ItemDataField("Elasticity Falloff", ref _target.data.ElasticityFalloff, dirtyMesh: false);
				ItemDataField("Friction", ref _target.data.Friction, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _target.data.Scatter, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Legacy Mode", ref _target.data.IsLegacy, dirtyMesh: false);
				ItemDataField("Collidable", ref _target.data.IsCollidable, dirtyMesh: false);
				ItemDataField("Is Dropped", ref _target.data.IsDropped, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _target.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _target.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
