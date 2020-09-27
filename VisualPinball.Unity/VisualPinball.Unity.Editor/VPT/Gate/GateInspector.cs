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

using UnityEngine;
using UnityEditor;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(GateAuthoring))]
	public class GateInspector : ItemInspector
	{
		private GateAuthoring _gate;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		private static string[] _gateTypeStrings = { "Wire: 'W'", "Wire: Rectangle", "Plate", "Long Plate" };
		private static int[] _gateTypeValues = { GateType.GateWireW, GateType.GateWireRectangle, GateType.GatePlate, GateType.GateLongPlate };

		private static readonly string TwoWayLabel = "Two Way";

		protected override void OnEnable()
		{
			base.OnEnable();
			_gate = target as GateAuthoring;
		}

		protected virtual void OnSceneGUI()
		{
			if (target is IEditableItemAuthoring editable) {
				var position = editable.GetEditorPosition();
				var transform = (target as MonoBehaviour).transform;
				if (transform != null && transform.parent != null) {
					position = transform.parent.TransformPoint(position);
					var axis = transform.TransformDirection(-Vector3.up); //Local direction of the gate gameObject is -up
					var worldScale = 0.5f * VpxConverter.GlobalScale;
					var scale = _gate.Item.Data.Length * worldScale;
					Handles.color = Color.white;
					Handles.DrawWireDisc(position, axis, scale);
					Color col = Color.grey;
					col.a = 0.25f;
					Handles.color = col;
					Handles.DrawSolidDisc(position, axis, scale);

					var arrowscale = worldScale * 100.0f;
					Handles.color = Color.white;
					Handles.ArrowHandleCap(-1, position, Quaternion.LookRotation(axis), arrowscale, EventType.Repaint);
					if (_gate.Item.Data.TwoWay) {
						Handles.ArrowHandleCap(-1, position, Quaternion.LookRotation(-axis), arrowscale, EventType.Repaint);
					}
				}
			}
		}
		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DropDownField("Type", ref _gate.Data.GateType, _gateTypeStrings, _gateTypeValues);
				ItemDataField("Visible", ref _gate.Data.IsVisible);
				ItemDataField("Show Bracket", ref _gate.Data.ShowBracket);
				MaterialField("Material", ref _gate.Data.Material);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _gate.Data.Center);
				ItemDataField("Length", ref _gate.Data.Length);
				ItemDataField("Height", ref _gate.Data.Height);
				ItemDataField("Rotation", ref _gate.Data.Rotation);
				ItemDataField("Open Angle", ref _gate.Data.AngleMax, dirtyMesh: false);
				ItemDataField("Close Angle", ref _gate.Data.AngleMin, dirtyMesh: false);
				SurfaceField("Surface", ref _gate.Data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Elasticity", ref _gate.Data.Elasticity, dirtyMesh: false);
				ItemDataField("Friction", ref _gate.Data.Friction, dirtyMesh: false);
				ItemDataField("Damping", ref _gate.Data.Damping, dirtyMesh: false);
				ItemDataField("Gravity Factor", ref _gate.Data.GravityFactor, dirtyMesh: false);
				ItemDataField("Collidable", ref _gate.Data.IsCollidable, dirtyMesh: false);
				ItemDataField(TwoWayLabel, ref _gate.Data.TwoWay, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _gate.Data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _gate.Data.TimerInterval, dirtyMesh: false);
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
