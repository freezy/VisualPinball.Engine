// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

using UnityEngine;
using UnityEditor;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(GateAuthoring)), CanEditMultipleObjects]
	public class GateInspector : ItemMainInspector<GateData, GateAuthoring>
	{
		private static readonly string[] GateTypeLabels = { "Wire: 'W'", "Wire: Rectangle", "Plate", "Long Plate" };
		private static readonly int[] GateTypeValues = { GateType.GateWireW, GateType.GateWireRectangle, GateType.GatePlate, GateType.GateLongPlate };

		private SerializedProperty _positionProperty;
		private SerializedProperty _rotationProperty;
		private SerializedProperty _lengthProperty;
		private SerializedProperty _surfaceProperty;

		public const string TwoWayLabel = "Two Way";

		protected override void OnEnable()
		{
			base.OnEnable();

			_positionProperty = serializedObject.FindProperty(nameof(GateAuthoring.Position));
			_rotationProperty = serializedObject.FindProperty(nameof(GateAuthoring._rotation));
			_lengthProperty = serializedObject.FindProperty(nameof(GateAuthoring._length));
			_surfaceProperty = serializedObject.FindProperty(nameof(GateAuthoring._surface));
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_positionProperty, updateTransforms: true);
			PropertyField(_rotationProperty, updateTransforms: true);
			PropertyField(_lengthProperty, updateTransforms: true);
			PropertyField(_surfaceProperty);

			base.OnInspectorGUI();

			serializedObject.ApplyModifiedProperties();
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
			if (target is IItemMainRenderableAuthoring editable) {
				var position = editable.GetEditorPosition();
				var transform = (target as MonoBehaviour).transform;
				if (transform != null && transform.parent != null) {
					position = transform.parent.TransformPoint(position);
					var axis = transform.TransformDirection(-Vector3.up); //Local direction of the gate gameObject is -up
					var worldScale = 0.5f * PlayfieldAuthoring.GlobalScale;
					var scale = MainComponent.Length * worldScale;
					Handles.color = Color.white;
					Handles.DrawWireDisc(position, axis, scale);
					Color col = Color.grey;
					col.a = 0.25f;
					Handles.color = col;
					Handles.DrawSolidDisc(position, axis, scale);

					var arrowScale = worldScale * 100.0f;
					Handles.color = Color.white;
					Handles.ArrowHandleCap(-1, position, Quaternion.LookRotation(axis), arrowScale, EventType.Repaint);
					var colliderComponent = MainComponent.GetComponent<GateColliderAuthoring>();
					if (colliderComponent && colliderComponent.TwoWay) {
						Handles.ArrowHandleCap(-1, position, Quaternion.LookRotation(-axis), arrowScale, EventType.Repaint);
					}
				}
			}
		}
	}
}
