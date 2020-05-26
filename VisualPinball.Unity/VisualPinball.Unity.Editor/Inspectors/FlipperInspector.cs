// ReSharper disable AssignmentInConditionalExpression

using System.Linq;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.VPT.Flipper;
using VisualPinball.Unity.VPT.Surface;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Editor.Inspectors
{
	[CustomEditor(typeof(FlipperBehavior))]
	public class FlipperInspector : ItemInspector
	{
		private bool _flipperFoldout = true;
		private bool _rubberFoldout = true;
		private bool _physicsFoldout = true;

		private Table _table;
		private FlipperBehavior _flipper;

		private SurfaceBehavior _surface;

		protected virtual void OnEnable()
		{
			_flipper = (FlipperBehavior) target;

			var tableComp = _flipper.gameObject.GetComponentInParent<TableBehavior>();
			_table = tableComp.Table;

			if (_flipper.data.Surface != null && _table.Surfaces.ContainsKey(_flipper.data.Surface)) {
				_surface = tableComp.gameObject.GetComponentsInChildren<SurfaceBehavior>(true)
					.FirstOrDefault(s => s.name == _flipper.data.Surface);
			}
		}

		public override void OnInspectorGUI()
		{
			EditorGUI.BeginChangeCheck();
			_surface = (SurfaceBehavior)EditorGUILayout.ObjectField("Surface", _surface, typeof(SurfaceBehavior), true);
			if (EditorGUI.EndChangeCheck()) {
				// TODO: undo for assigning surface, the member var makes this weird, maybe we show the data name here instead?
				_flipper.data.Surface = _surface != null ? _surface.name : "";
				_flipper.RebuildMeshes();
			}

			// flipper mesh
			if (_flipperFoldout = EditorGUILayout.Foldout(_flipperFoldout, "Flipper")) {
				EditorGUI.indentLevel++;
				ItemDataField("Base Radius", ref _flipper.data.BaseRadius);
				ItemDataField("End Radius", ref _flipper.data.EndRadius);
				ItemDataField("Length", ref _flipper.data.FlipperRadius);
				ItemDataField("Height", ref _flipper.data.Height);
				EditorGUI.indentLevel--;
			}

			// rubber mesh
			if (_rubberFoldout = EditorGUILayout.Foldout(_rubberFoldout, "Rubber")) {
				EditorGUI.indentLevel++;
				ItemDataField("Height", ref _flipper.data.RubberHeight);
				ItemDataField("Thickness", ref _flipper.data.RubberThickness);
				ItemDataField("Width", ref _flipper.data.RubberWidth);
				EditorGUI.indentLevel--;
			}

			// physics
			if (_physicsFoldout = EditorGUILayout.Foldout(_physicsFoldout, "Physics")) {
				EditorGUI.indentLevel++;
				ItemDataField("Mass", ref _flipper.data.Mass, dirtyMesh: false);
				ItemDataField("Strength", ref _flipper.data.Strength, dirtyMesh: false);
				ItemDataField("Elasticity", ref _flipper.data.Elasticity, dirtyMesh: false);
				ItemDataField("Elasticity Falloff", ref _flipper.data.ElasticityFalloff, dirtyMesh: false);
				ItemDataField("Friction", ref _flipper.data.Friction, dirtyMesh: false);
				ItemDataField("Return Strength", ref _flipper.data.Return, dirtyMesh: false);
				ItemDataField("Coil Ramp Up", ref _flipper.data.RampUp, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _flipper.data.Scatter, dirtyMesh: false);
				ItemDataField("EOS Torque", ref _flipper.data.TorqueDamping, dirtyMesh: false);
				ItemDataField("EOS Torque Angle", ref _flipper.data.TorqueDampingAngle, dirtyMesh: false);
				EditorGUI.indentLevel--;
			}

			_flipper.RebuildMeshes();
		}
	}
}
