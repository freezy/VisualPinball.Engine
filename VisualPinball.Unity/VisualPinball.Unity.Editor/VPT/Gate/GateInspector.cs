// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

// ReSharper disable AssignmentInConditionalExpression

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(GateComponent)), CanEditMultipleObjects]
	public class GateInspector : MainInspector<GateData, GateComponent>
	{
		private const string MeshFbx = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Art/Meshes/Gate/Gate Meshes.fbx";

		private static readonly Dictionary<string, int> WireTypeMap = new Dictionary<string, int> {
			{ "Long Plate", GateType.GateLongPlate },
			{ "Plate", GateType.GatePlate },
			{ "Wire Rectangle", GateType.GateWireRectangle },
			{ "Wire W", GateType.GateWireW },
		};

		private SerializedProperty _meshProperty;
		private SerializedProperty _typeProperty;

		public const string TwoWayLabel = "Two Way";

		protected override void OnEnable()
		{
			base.OnEnable();

			_meshProperty = serializedObject.FindProperty(nameof(GateComponent._meshName));
			_typeProperty = serializedObject.FindProperty(nameof(GateComponent._type));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			BeginEditing();

			OnPreInspectorGUI();

			// position
			EditorGUI.BeginChangeCheck();
			var newPos = EditorGUILayout.Vector3Field(new GUIContent("Position", "Position of the gate on the playfield, relative to its parent."), MainComponent.Position);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Gate Position");
				MainComponent.Position = newPos;
			}

			// start angle
			EditorGUI.BeginChangeCheck();
			var newAngle = EditorGUILayout.Slider(new GUIContent("Rotation", "Angle of the gate on the playfield (z-axis rotation)"), MainComponent.Rotation, -180f, 180f);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Flipper Start Angle");
				MainComponent.Rotation = newAngle;
			}

			// length
			EditorGUI.BeginChangeCheck();
			var newLength = EditorGUILayout.Slider(new GUIContent("Length", "How much the gate is scaled, in percent."), MainComponent.Length, 10f, 250f);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Gate Length");
				MainComponent.Length = newLength;
			}

			EndEditing();
		}

		protected override void FinishEdit(string label, bool dirtyMesh = true)
		{
			if (label == TwoWayLabel) {
				SceneView.RepaintAll();
			}
			base.FinishEdit(label, dirtyMesh);
		}

		protected void OnSceneGUI()
		{
			if (target is not GateComponent gateComponent) {
				return;
			}

			var transform = gateComponent.transform;
			if (transform == null || transform.parent == null) {
				return;
			}

			var position = transform.position;
			var axis = transform.TransformDirection(Vector3.forward); //Local direction of the gate gameObject is -up
			var scale = gateComponent.Length / 5000;

			Handles.matrix = Matrix4x4.identity;
			Handles.color = Color.white;
			Handles.DrawWireDisc(position, axis, scale);
			var col = Color.grey;
			col.a = 0.25f;
			Handles.color = col;
			Handles.DrawSolidDisc(position, axis, scale);

			const float arrowScale = 0.05f;
			Handles.color = Color.white;
			Handles.ArrowHandleCap(-1, position, Quaternion.LookRotation(axis), arrowScale, EventType.Repaint);
			var colliderComponent = gateComponent.GetComponent<GateColliderComponent>();
			if (colliderComponent && colliderComponent.TwoWay) {
				Handles.ArrowHandleCap(-1, position, Quaternion.LookRotation(-axis), arrowScale, EventType.Repaint);
			}
		}
	}
}
