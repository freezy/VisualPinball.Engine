// ReSharper disable AssignmentInConditionalExpression

using System.Linq;
using NLog;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Components;
using Logger = NLog.Logger;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(VisualPinballFlipper))]
	[CanEditMultipleObjects]
	public class VisualPinballFlipperInspector : UnityEditor.Editor
	{
		private bool _flipperFoldout = true;
		private bool _rubberFoldout = true;
		private bool _physicsFoldout = true;

		private Table _table;
		private VisualPinballFlipper _flipper;
		private Transform _transform;

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private VisualPinballSurface _surface;



		private void OnEnable()
		{
			_flipper = (VisualPinballFlipper) target;

			var tableComp = _flipper.gameObject.GetComponentInParent<VisualPinballTable>();
			_table = tableComp.Table;
			_transform = _flipper.gameObject.GetComponent<Transform>();

			if (_flipper.data.Surface != null && _table.Surfaces.ContainsKey(_flipper.data.Surface)) {
				_surface = tableComp.gameObject.GetComponentsInChildren<VisualPinballSurface>(true)
					.FirstOrDefault(s => s.name == _flipper.data.Surface);
			}
		}

		public override void OnInspectorGUI()
		{
			EditorGUI.BeginChangeCheck();
			_surface = (VisualPinballSurface)EditorGUILayout.ObjectField("Surface", _surface, typeof(VisualPinballSurface), true);
			if (EditorGUI.EndChangeCheck()) {
				_flipper.data.Surface = _surface != null ? _surface.name : "";
				_flipper.RebuildMeshes();
			}

			EditorGUI.BeginChangeCheck();

			// flipper mesh
			if (_flipperFoldout = EditorGUILayout.Foldout(_flipperFoldout, "Flipper")) {
				EditorGUI.indentLevel++;
				_flipper.data.BaseRadius = EditorGUILayout.FloatField("Base Radius", _flipper.data.BaseRadius);
				_flipper.data.EndRadius = EditorGUILayout.FloatField("End Radius", _flipper.data.EndRadius);
				_flipper.data.FlipperRadius = EditorGUILayout.FloatField("Length", _flipper.data.FlipperRadius);
				_flipper.data.Height = EditorGUILayout.FloatField("Height", _flipper.data.Height);
				EditorGUI.indentLevel--;
			}

			// rubber mesh
			if (_rubberFoldout = EditorGUILayout.Foldout(_rubberFoldout, "Rubber")) {
				EditorGUI.indentLevel++;
				_flipper.data.RubberHeight = EditorGUILayout.FloatField("Height", _flipper.data.RubberHeight);
				_flipper.data.RubberThickness = EditorGUILayout.FloatField("Thickness", _flipper.data.RubberThickness);
				_flipper.data.RubberWidth = EditorGUILayout.FloatField("Width", _flipper.data.RubberWidth);
				EditorGUI.indentLevel--;
			}

			if (EditorGUI.EndChangeCheck()) {
				_flipper.RebuildMeshes();
			}

			// physics
			if (_physicsFoldout = EditorGUILayout.Foldout(_physicsFoldout, "Physics")) {
				EditorGUI.indentLevel++;
				_flipper.data.Mass = EditorGUILayout.FloatField("Mass", _flipper.data.Mass);
				_flipper.data.Strength = EditorGUILayout.FloatField("Strength", _flipper.data.Strength);
				_flipper.data.Elasticity = EditorGUILayout.FloatField("Elasticity", _flipper.data.Elasticity);
				_flipper.data.ElasticityFalloff = EditorGUILayout.FloatField("Elasticity Falloff", _flipper.data.ElasticityFalloff);
				_flipper.data.Friction = EditorGUILayout.FloatField("Friction", _flipper.data.Friction);
				_flipper.data.Return = EditorGUILayout.FloatField("Return Strength", _flipper.data.Return);
				_flipper.data.RampUp = EditorGUILayout.FloatField("Coil Ramp Up", _flipper.data.RampUp);
				_flipper.data.Scatter = EditorGUILayout.FloatField("Scatter Angle", _flipper.data.Scatter);
				_flipper.data.TorqueDamping = EditorGUILayout.FloatField("EOS Torque", _flipper.data.TorqueDamping);
				_flipper.data.TorqueDampingAngle = EditorGUILayout.FloatField("EOS Torque Angle", _flipper.data.TorqueDampingAngle);
				EditorGUI.indentLevel--;
			}

			if (GUILayout.Button("Flip Up")) {

			}
		}
	}
}
