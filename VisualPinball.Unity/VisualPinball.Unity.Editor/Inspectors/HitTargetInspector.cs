﻿using UnityEditor;
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

		protected override void OnEnable()
		{
			base.OnEnable();
			_target = target as HitTargetBehavior;
		}

		public override void OnInspectorGUI()
		{
			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				ItemDataField("Drop Speed", ref _target.data.DropSpeed, dirtyMesh: false);
				ItemDataField("Raise Delay", ref _target.data.RaiseDelay, dirtyMesh: false);
				ItemDataField("Depth Bias", ref _target.data.DepthBias, dirtyMesh: false);
				ItemDataField("Visible", ref _target.data.IsVisible);
				ItemDataField("Reflection Enabled", ref _target.data.IsReflectionEnabled);
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
				ItemDataField("Physics Material", ref _target.data.PhysicsMaterial, dirtyMesh: false);
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
