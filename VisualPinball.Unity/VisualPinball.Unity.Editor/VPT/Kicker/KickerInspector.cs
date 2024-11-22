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

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(KickerComponent)), CanEditMultipleObjects]
	public class KickerInspector : MainInspector<KickerData, KickerComponent>
	{
		private const string MeshFolder = "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Art/Meshes/Kicker";

		private static readonly Dictionary<string, int> TypeMap = new() {
			{ "Cup 1", KickerType.KickerCup },
			{ "Cup 2", KickerType.KickerCup2 },
			{ "Gottlieb", KickerType.KickerGottlieb },
			{ "Hole", KickerType.KickerHole },
			{ "Simple Hole", KickerType.KickerHoleSimple },
			{ "Williams", KickerType.KickerWilliams },
			{ CustomMeshLabel, KickerType.KickerInvisible },
		};

		private SerializedProperty _orientationProperty;
		private SerializedProperty _surfaceProperty;
		private SerializedProperty _kickerTypeProperty;
		private SerializedProperty _meshNameProperty;
		private SerializedProperty _coilsProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_orientationProperty = serializedObject.FindProperty(nameof(KickerComponent.Orientation));
			_surfaceProperty = serializedObject.FindProperty(nameof(KickerComponent._surface));
			_kickerTypeProperty = serializedObject.FindProperty(nameof(KickerComponent.KickerType));
			_meshNameProperty = serializedObject.FindProperty(nameof(KickerComponent.MeshName));
			_coilsProperty = serializedObject.FindProperty(nameof(KickerComponent.Coils));
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
			var newPos = EditorGUILayout.Vector2Field(new GUIContent("Position", "Position of the kicker on the playfield, relative to its parent."), MainComponent.Position);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Kicker Position");
				MainComponent.Position = newPos;
			}

			// radius
			EditorGUI.BeginChangeCheck();
			var newRadius = EditorGUILayout.FloatField(new GUIContent("Radius", "Kicker radius. Scales the mesh accordingly."), MainComponent.Radius);
			if (EditorGUI.EndChangeCheck()) {
				Undo.RecordObject(MainComponent.transform, "Change Kicker Radius");
				MainComponent.Radius = newRadius;
			}

			if (MainComponent.KickerType == KickerType.KickerCup ||
			    MainComponent.KickerType == KickerType.KickerWilliams) {
				PropertyField(_orientationProperty, updateTransforms: true);
			}
			PropertyField(_surfaceProperty, updateTransforms: true);

			MeshDropdownProperty("Mesh", _meshNameProperty, MeshFolder, MainComponent.gameObject, _kickerTypeProperty, TypeMap);

			PropertyField(_coilsProperty);

			base.OnInspectorGUI();

			EndEditing();
		}

		private void OnSceneGUI()
		{
			if (Event.current.type != EventType.Repaint) {
				return;
			}

			Handles.color = Color.cyan;
			Handles.matrix = Matrix4x4.identity;
			var transform = MainComponent.transform;
			var localPos = MainComponent.GetEditorPosition();
			localPos.z = MainComponent.PositionZ;

			var worldPos = transform.parent == null ? localPos : localPos.TranslateToWorld();

			foreach (var coil in MainComponent.Coils) {
				var from = MainComponent.GetBallCreationPosition().ToUnityVector3();
				var l = coil.Speed == 0 ? 1f : 20f * coil.Speed;
				var dir = new Vector3(
					l * math.sin(math.radians(coil.Angle)),
					l * math.sin(math.radians(coil.Inclination)),
					l * math.cos(math.radians(coil.Angle))
				);
				var to = from + dir;
				var worldDir = transform.TransformDirection(math.normalize( to - from));
				var length = coil.Speed == 0 ? 0.1f : coil.Speed / 10f;

				Handles.ArrowHandleCap(-1, worldPos, Quaternion.LookRotation(worldDir), length, EventType.Repaint);
			}
		}
	}
}
