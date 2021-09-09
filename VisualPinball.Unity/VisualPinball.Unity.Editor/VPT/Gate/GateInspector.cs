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

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(GateComponent)), CanEditMultipleObjects]
	public class GateInspector : ItemMainInspector<GateData, GateComponent>
	{
		private const string MeshFolder = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Art/Meshes/Gate/Wire";

		private static readonly Dictionary<string, int> TypeMap = new Dictionary<string, int> {
			{ "Long Plate", GateType.GateLongPlate },
			{ "Plate", GateType.GatePlate },
			{ "Wire Rectangle", GateType.GateWireRectangle },
			{ "Wire W", GateType.GateWireW },
		};

		private SerializedProperty _positionProperty;
		private SerializedProperty _rotationProperty;
		private SerializedProperty _lengthProperty;
		private SerializedProperty _surfaceProperty;
		private SerializedProperty _meshProperty;
		private SerializedProperty _typeProperty;

		public const string TwoWayLabel = "Two Way";

		protected override void OnEnable()
		{
			base.OnEnable();

			_positionProperty = serializedObject.FindProperty(nameof(GateComponent.Position));
			_rotationProperty = serializedObject.FindProperty(nameof(GateComponent._rotation));
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

			serializedObject.Update();

			OnPreInspectorGUI();

			PropertyField(_positionProperty, updateTransforms: true);
			PropertyField(_rotationProperty, updateTransforms: true);
			PropertyField(_lengthProperty, updateTransforms: true);
			PropertyField(_surfaceProperty);

			var wire = MainComponent.transform.Find(GateComponent.WireObjectName);
			if (wire != null) {
				MeshDropdownProperty("Mesh", _meshProperty, MeshFolder, wire.gameObject, _typeProperty, TypeMap);
			}

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
			if (target is IItemMainRenderableComponent editable) {
				var position = editable.GetEditorPosition();
				var transform = (target as MonoBehaviour).transform;
				if (transform != null && transform.parent != null) {
					position = transform.parent.TransformPoint(position);
					var axis = transform.TransformDirection(-Vector3.up); //Local direction of the gate gameObject is -up
					var worldScale = 0.5f * Unity.PlayfieldComponent.GlobalScale;
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
					var colliderComponent = MainComponent.GetComponent<GateColliderComponent>();
					if (colliderComponent && colliderComponent.TwoWay) {
						Handles.ArrowHandleCap(-1, position, Quaternion.LookRotation(-axis), arrowScale, EventType.Repaint);
					}
				}
			}
		}
	}
}
