// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
	[CustomEditor(typeof(GateAuthoring))]
	public class GateInspector : ItemMainInspector<Gate, GateData, GateAuthoring>
	{
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics;
		private bool _foldoutMisc;

		private static readonly string[] GateTypeLabels = { "Wire: 'W'", "Wire: Rectangle", "Plate", "Long Plate" };
		private static readonly int[] GateTypeValues = { GateType.GateWireW, GateType.GateWireRectangle, GateType.GatePlate, GateType.GateLongPlate };

		public const string TwoWayLabel = "Two Way";

		protected void OnSceneGUI()
		{
			if (target is IItemMainAuthoring editable) {
				var position = editable.GetEditorPosition();
				var transform = (target as MonoBehaviour).transform;
				if (transform != null && transform.parent != null) {
					position = transform.parent.TransformPoint(position);
					var axis = transform.TransformDirection(-Vector3.up); //Local direction of the gate gameObject is -up
					var worldScale = 0.5f * VpxConverter.GlobalScale;
					var scale = Data.Length * worldScale;
					Handles.color = Color.white;
					Handles.DrawWireDisc(position, axis, scale);
					Color col = Color.grey;
					col.a = 0.25f;
					Handles.color = col;
					Handles.DrawSolidDisc(position, axis, scale);

					var arrowScale = worldScale * 100.0f;
					Handles.color = Color.white;
					Handles.ArrowHandleCap(-1, position, Quaternion.LookRotation(axis), arrowScale, EventType.Repaint);
					if (Data.TwoWay) {
						Handles.ArrowHandleCap(-1, position, Quaternion.LookRotation(-axis), arrowScale, EventType.Repaint);
					}
				}
			}
		}

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			ItemDataField("Position", ref Data.Center);
			SurfaceField("Surface", ref Data.Surface);

			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DropDownField("Type", ref Data.GateType, GateTypeLabels, GateTypeValues);
				ItemDataField("Show Bracket", ref Data.ShowBracket);
				MaterialField("Material", ref Data.Material);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Geometry")) {

				ItemDataField("Length", ref Data.Length);
				ItemDataField("Height", ref Data.Height);
				ItemDataField("Rotation", ref Data.Rotation);
				ItemDataField("Open Angle", ref Data.AngleMax, false);
				ItemDataField("Close Angle", ref Data.AngleMin, false);

			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Elasticity", ref Data.Elasticity, false);
				ItemDataField("Friction", ref Data.Friction, false);
				ItemDataField("Damping", ref Data.Damping, false);
				ItemDataField("Gravity Factor", ref Data.GravityFactor, false);
				ItemDataField("Collidable", ref Data.IsCollidable, false);
				ItemDataField(TwoWayLabel, ref Data.TwoWay, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref Data.IsTimerEnabled, false);
				ItemDataField("Timer Interval", ref Data.TimerInterval, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}

		protected override void FinishEdit(string label, bool dirtyMesh = true)
		{
			if (label == TwoWayLabel) {
				SceneView.RepaintAll();
			}
			base.FinishEdit(label, dirtyMesh);
		}
	}
}
