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
	[CanEditMultipleObjects]
	public class FlipperInspector : UnityEditor.Editor
	{
		private bool _flipperFoldout = true;
		private bool _rubberFoldout = true;
		private bool _physicsFoldout = true;

		private Table _table;
		private FlipperBehavior _flipper;
		private Transform _transform;

		private SurfaceBehavior _surface;

		protected virtual void OnEnable()
		{
			_flipper = (FlipperBehavior) target;

			var tableComp = _flipper.gameObject.GetComponentInParent<TableBehavior>();
			_table = tableComp.Table;
			_transform = _flipper.gameObject.GetComponent<Transform>();

			if (_flipper.data.Surface != null && _table.Surfaces.ContainsKey(_flipper.data.Surface)) {
				_surface = tableComp.gameObject.GetComponentsInChildren<SurfaceBehavior>(true)
					.FirstOrDefault(s => s.name == _flipper.data.Surface);
			}
		}

		protected virtual void OnDisable()
		{
			// restore tools
			Tools.hidden = false;
		}

		protected virtual void OnSceneGUI()
		{
			if (Tools.current == Tool.Rotate) {
				// if the rotation tool is active turn off the default handles
				Tools.hidden = true;

				var flipper = target as FlipperBehavior;
				var pos = flipper.transform.position;

				EditorGUI.BeginChangeCheck();
				Handles.color = Handles.zAxisColor;
				var rot = Handles.Disc(flipper.transform.rotation, pos, flipper.transform.forward, HandleUtility.GetHandleSize(pos), false, 10f);

				if (EditorGUI.EndChangeCheck()) {
					Undo.RecordObject(flipper.transform, "Flipper Rotate");
					flipper.transform.rotation = rot;
					var localRotZ = flipper.transform.localEulerAngles.z;
					flipper.transform.localRotation = Quaternion.Euler(0f, 0f, localRotZ);
				}
			} else if(Tools.current == Tool.Move) {
				Tools.hidden = true;

				var flipper = target as FlipperBehavior;
				var pos = flipper.transform.position;

				EditorGUI.BeginChangeCheck();
				Handles.color = Handles.xAxisColor;
				var newPos = Handles.Slider(pos, flipper.transform.right);
				if (EditorGUI.EndChangeCheck()) {
					FinishMove(flipper, newPos);
				}

				EditorGUI.BeginChangeCheck();
				Handles.color = Handles.yAxisColor;
				newPos = Handles.Slider(pos, flipper.transform.up);
				if (EditorGUI.EndChangeCheck()) {
					FinishMove(flipper, newPos);
				}

				EditorGUI.BeginChangeCheck();
				Handles.color = Handles.zAxisColor;
				float size = HandleUtility.GetHandleSize(pos) * 0.2f;
				newPos = Handles.Slider2D(pos, flipper.transform.forward, flipper.transform.right, flipper.transform.up, size, Handles.RectangleHandleCap, 0f);
				if (EditorGUI.EndChangeCheck()) {
					FinishMove(flipper, newPos);
				}
			} else {
				Tools.hidden = false;
				return;
			}
		}

		private void FinishMove(FlipperBehavior flipper, Vector3 newPos)
		{
			Undo.RecordObject(flipper.transform, "Flipper Move");
			flipper.transform.position = newPos;
			var localPos = flipper.transform.localPosition;
			localPos.z = 0f;
			flipper.transform.localPosition = localPos;
		}

		public override void OnInspectorGUI()
		{
			EditorGUI.BeginChangeCheck();
			_surface = (SurfaceBehavior)EditorGUILayout.ObjectField("Surface", _surface, typeof(SurfaceBehavior), true);
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
		}
	}
}
