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

using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(KickerComponent)), CanEditMultipleObjects]
	public class KickerInspector : MainInspector<KickerData, KickerComponent>
	{
		private SerializedProperty _orientationProperty;
		private SerializedProperty _coilsProperty;

		protected override void OnEnable()
		{
			base.OnEnable();

			_orientationProperty = serializedObject.FindProperty(nameof(KickerComponent.Orientation));
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
			var newPos = EditorGUILayout.Vector3Field(new GUIContent("Position", "Position of the kicker on the playfield, relative to its parent."), MainComponent.Position);
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

			PropertyField(_orientationProperty, updateTransforms: true);
			PropertyField(_coilsProperty);

			base.OnInspectorGUI();

			EndEditing();
		}

		private void OnSceneGUI()
		{
			if (Event.current.type != EventType.Repaint) {
				return;
			}

			var playfield = MainComponent.GetComponentInParent<PlayfieldComponent>();
			var transform = MainComponent.transform;
			var worldToPlayfield = playfield ? (float4x4)playfield.transform.worldToLocalMatrix : float4x4.identity;
			var localToPlayfield = Physics.GetLocalToPlayfieldMatrixInVpx(transform.localToWorldMatrix, worldToPlayfield);
			var rotation = new quaternion(new float3x3(
				math.normalizesafe(localToPlayfield.c0.xyz, new float3(1f, 0f, 0f)),
				math.normalizesafe(localToPlayfield.c1.xyz, new float3(0f, 1f, 0f)),
				math.normalizesafe(localToPlayfield.c2.xyz, new float3(0f, 0f, 1f))
			));

			Handles.color = Color.cyan;
			Handles.matrix = Matrix4x4.identity;
			foreach (var coil in MainComponent.Coils) {
				var previewSpeed = math.abs(coil.Speed) < Collider.Tolerance ? 1f : coil.Speed;
				var velocity = math.mul(rotation, KickerApi.GetKickVelocity(math.radians(coil.Angle), previewSpeed, coil.Inclination));
				var playfieldDirection = ((Vector3)velocity).TranslateToWorld();
				var worldDirection = playfield ? playfield.transform.TransformDirection(playfieldDirection) : playfieldDirection;
				worldDirection = math.normalizesafe((float3)worldDirection, new float3(0f, 1f, 0f));
				var length = math.abs(coil.Speed) < Collider.Tolerance ? 0.1f : math.abs(coil.Speed) / 10f;

				Handles.ArrowHandleCap(-1, transform.position, Quaternion.LookRotation(worldDirection), length, EventType.Repaint);
			}
		}
	}
}
