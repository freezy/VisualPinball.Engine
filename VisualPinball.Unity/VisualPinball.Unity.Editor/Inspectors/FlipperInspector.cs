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

		protected virtual void OnDisable()
		{
			// restore tools
			Tools.hidden = false;
		}

		protected virtual void OnSceneGUI()
		{
			switch (Tools.current) {
				case Tool.Rotate:
					HandleRotationTool();
					break;

				case Tool.Move:
					HandleMoveTool();
					break;

				default:
					Tools.hidden = false;
					break;
			}
		}

		private void HandleRotationTool()
		{
			// if the rotation tool is active turn off the default handles
			Tools.hidden = true;

			if (_flipper == null) {
				return;
			}
			var flipperTransform = _flipper.transform;
			var pos = flipperTransform.position;

			EditorGUI.BeginChangeCheck();
			Handles.color = Handles.zAxisColor;
			var rot = Handles.Disc(flipperTransform.rotation, pos, flipperTransform.forward, HandleUtility.GetHandleSize(pos), false, 10f);

			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(flipperTransform, "Flipper Rotate");
				flipperTransform.rotation = rot;
				var localRotZ = flipperTransform.localEulerAngles.z;
				flipperTransform.localRotation = Quaternion.Euler(0f, 0f, localRotZ);
			}
		}

		private void HandleMoveTool()
		{
			Tools.hidden = true;

			if (_flipper == null) {
				return;
			}
			var flipperTransform = _flipper.transform;
			var pos = flipperTransform.position;

			EditorGUI.BeginChangeCheck();
			Handles.color = Handles.xAxisColor;
			var newPos = Handles.Slider(pos, flipperTransform.right);
			if (EditorGUI.EndChangeCheck()) {
				FinishMove(flipperTransform, newPos);
			}

			EditorGUI.BeginChangeCheck();
			Handles.color = Handles.yAxisColor;
			newPos = Handles.Slider(pos, flipperTransform.up);
			if (EditorGUI.EndChangeCheck()) {
				FinishMove(flipperTransform, newPos);
			}

			EditorGUI.BeginChangeCheck();
			Handles.color = Handles.zAxisColor;
			newPos = Handles.Slider2D(
				pos,
				flipperTransform.forward,
				flipperTransform.right,
				flipperTransform.up,
				HandleUtility.GetHandleSize(pos) * 0.2f,
				Handles.RectangleHandleCap,
			0f);
			if (EditorGUI.EndChangeCheck()) {
				FinishMove(flipperTransform, newPos);
			}
		}

		private static void FinishMove(Transform flipperTransform, Vector3 newPos)
		{
			Undo.RecordObject(flipperTransform, "Flipper Move");
			flipperTransform.position = newPos;
			var localPos = flipperTransform.localPosition;
			localPos.z = 0f;
			flipperTransform.localPosition = localPos;
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
