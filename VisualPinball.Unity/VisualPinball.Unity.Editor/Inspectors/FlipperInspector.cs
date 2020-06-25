// ReSharper disable AssignmentInConditionalExpression

using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Editor.Utils;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Surface;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(FlipperBehavior))]
	public class FlipperInspector : ItemInspector
	{
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		private FlipperBehavior _flipper;

		protected override void OnEnable()
		{
			base.OnEnable();
			_flipper = (FlipperBehavior)target;
		}

		public override void OnInspectorGUI()
		{
			base.OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DataFieldUtils.ItemDataField("Rubber Thickness", ref _flipper.data.RubberThickness, FinishEdit);
				DataFieldUtils.ItemDataField("Rubber Offset Height", ref _flipper.data.RubberHeight, FinishEdit);
				DataFieldUtils.ItemDataField("Rubber Width", ref _flipper.data.RubberWidth, FinishEdit);
				DataFieldUtils.ItemDataField("Visible", ref _flipper.data.IsVisible, FinishEdit);
				DataFieldUtils.ItemDataField("Enabled", ref _flipper.data.IsEnabled, FinishEdit);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				DataFieldUtils.ItemDataField("", ref _flipper.data.Center, FinishEdit);
				DataFieldUtils.ItemDataField("Base Radius", ref _flipper.data.BaseRadius, FinishEdit);
				DataFieldUtils.ItemDataField("End Radius", ref _flipper.data.EndRadius, FinishEdit);
				DataFieldUtils.ItemDataField("Length", ref _flipper.data.FlipperRadius, FinishEdit);
				DataFieldUtils.ItemDataField("Start Angle", ref _flipper.data.StartAngle, FinishEdit);
				DataFieldUtils.ItemDataField("End Angle", ref _flipper.data.EndAngle, FinishEdit);
				DataFieldUtils.ItemDataField("Height", ref _flipper.data.Height, FinishEdit);
				DataFieldUtils.ItemDataField("Max. Difficulty Length", ref _flipper.data.FlipperRadiusMax, FinishEdit);
				SurfaceField("Surface", ref _flipper.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				DataFieldUtils.ItemDataField("Mass", ref _flipper.data.Mass, FinishEdit, false);
				DataFieldUtils.ItemDataField("Strength", ref _flipper.data.Strength, FinishEdit, false);
				DataFieldUtils.ItemDataField("Elasticity", ref _flipper.data.Elasticity, FinishEdit, false);
				DataFieldUtils.ItemDataField("Elasticity Falloff", ref _flipper.data.ElasticityFalloff, FinishEdit, false);
				DataFieldUtils.ItemDataField("Friction", ref _flipper.data.Friction, FinishEdit, false);
				DataFieldUtils.ItemDataField("Return Strength", ref _flipper.data.Return, FinishEdit, false);
				DataFieldUtils.ItemDataField("Coil Ramp Up", ref _flipper.data.RampUp, FinishEdit, false);
				DataFieldUtils.ItemDataField("Scatter Angle", ref _flipper.data.Scatter, FinishEdit, false);
				DataFieldUtils.ItemDataField("EOS Torque", ref _flipper.data.TorqueDamping, FinishEdit, false);
				DataFieldUtils.ItemDataField("EOS Torque Angle", ref _flipper.data.TorqueDampingAngle, FinishEdit, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				DataFieldUtils.ItemDataField("Timer Enabled", ref _flipper.data.IsTimerEnabled, FinishEdit, false);
				DataFieldUtils.ItemDataField("Timer Interval", ref _flipper.data.TimerInterval, FinishEdit, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
