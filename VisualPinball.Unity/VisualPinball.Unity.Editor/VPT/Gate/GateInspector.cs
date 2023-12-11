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
		private const string MeshFolder = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Art/Meshes/Gate/Wire";

		private static readonly Dictionary<string, int> TypeMap = new Dictionary<string, int> {
			{ "Long Plate", GateType.GateLongPlate },
			{ "Plate", GateType.GatePlate },
			{ "Wire Rectangle", GateType.GateWireRectangle },
			{ "Wire W", GateType.GateWireW },
		};

		private SerializedProperty _lengthProperty;
		private SerializedProperty _surfaceProperty;
		private SerializedProperty _meshProperty;
		private SerializedProperty _typeProperty;

		public const string TwoWayLabel = "Two Way";

		protected override void OnEnable()
		{
			base.OnEnable();

			_lengthProperty = serializedObject.FindProperty(nameof(GateComponent._length));
			_surfaceProperty = serializedObject.FindProperty(nameof(GateComponent._surface));
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

			PropertyField(_lengthProperty, updateTransforms: true);
			PropertyField(_surfaceProperty);

			var wire = MainComponent.transform.Find(GateComponent.WireObjectName);
			if (wire != null) {
				MeshDropdownProperty("Mesh", _meshProperty, MeshFolder, wire.gameObject, _typeProperty, TypeMap);
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
			if (target is not IMainRenderableComponent editable) {
				return;
			}
			
			var transform = (target as MonoBehaviour)?.transform;
			if (transform == null || transform.parent == null) {
				return;
			}
			
			var position = editable.GetEditorPosition();
			position = transform.parent.TransformPoint(position);
			var axis = transform.TransformDirection(-Vector3.up); //Local direction of the gate gameObject is -up
			var scale = MainComponent.Length / 5000;
			Handles.color = Color.white;
			Handles.DrawWireDisc(position, axis, scale);
			var col = Color.grey;
			col.a = 0.25f;
			Handles.color = col;
			Handles.DrawSolidDisc(position, axis, scale);

			const float arrowScale = 0.05f;
			Handles.color = Color.white;
			Handles.ArrowHandleCap(-1, position, Quaternion.LookRotation(axis), arrowScale, EventType.Repaint);
			var colliderComponent = MainComponent.GetComponent<GateColliderComponent>();
			if (colliderComponent && colliderComponent.TwoWay) {
				Handles.ArrowHandleCap(-1, position, Quaternion.LookRotation(-axis), arrowScale, EventType.Repaint);
			}
		}
	}
}
