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

using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Trigger;
using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(FlipperAuthoring))]
	public class FlipperInspector : ItemMainInspector<Flipper, FlipperData, FlipperAuthoring>
	{
		private bool _foldoutPosition = true;
		private bool _foldoutBaseMesh;
		private bool _foldoutRubberMesh;
		private bool _foldoutPhysics;
		private bool _foldoutMisc;

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			ItemDataField("Position", ref Data.Center);
			SurfaceField("Surface", ref Data.Surface);

			OnPreInspectorGUI();

			ItemDataField("Enabled", ref Data.IsEnabled);

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Geometry")) {
				ItemDataField("Base Radius", ref Data.BaseRadius);
				ItemDataField("End Radius", ref Data.EndRadius);
				ItemDataField("Length", ref Data.FlipperRadius);
				ItemDataField("Start Angle", ref Data.StartAngle);
				ItemDataField("End Angle", ref Data.EndAngle);
				ItemDataField("Height", ref Data.Height);
				ItemDataField("Max. Difficulty Length", ref Data.FlipperRadiusMax);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutBaseMesh = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutBaseMesh, "Base Mesh")) {
				TextureField("Image", ref Data.Image);
				MaterialField("Material", ref Data.Material);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutRubberMesh = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutRubberMesh, "Rubber Mesh")) {
				MaterialField("Rubber Material", ref Data.RubberMaterial);
				ItemDataField("Rubber Thickness", ref Data.RubberThickness, onChanged: ItemAuthoring.OnRubberWidthUpdated);
				ItemDataField("Rubber Offset Height", ref Data.RubberHeight);
				ItemDataField("Rubber Width", ref Data.RubberWidth);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Mass", ref Data.Mass, false);
				ItemDataField("Strength", ref Data.Strength, false);
				ItemDataField("Elasticity", ref Data.Elasticity, false);
				ItemDataField("Elasticity Falloff", ref Data.ElasticityFalloff, false);
				ItemDataField("Friction", ref Data.Friction, false);
				ItemDataField("Return Strength", ref Data.Return, false);
				ItemDataField("Coil Ramp Up", ref Data.RampUp, false);
				ItemDataField("Scatter Angle", ref Data.Scatter, false);
				ItemDataField("EOS Torque", ref Data.TorqueDamping, false);
				ItemDataField("EOS Torque Angle", ref Data.TorqueDampingAngle, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref Data.IsTimerEnabled, false);
				ItemDataField("Timer Interval", ref Data.TimerInterval, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (GUILayout.Button("Setup nFozzy Corrections"))
			{
				var fa = target as FlipperAuthoring;

				// Get table reference
				TableAuthoring table = fa.GetComponentInParent<TableAuthoring>();
				if (fa != null && table != null)
				{

					GameObject nFozzyCorrection = new GameObject(fa.name + "_nFozzy");
					nFozzyCorrection.transform.parent = fa.transform.parent;
					nFozzyCorrection.transform.localScale = fa.transform.localScale;
					nFozzyCorrection.transform.localPosition = fa.transform.localPosition;
					nFozzyCorrection.transform.rotation = table.transform.rotation;

					TriggerData data = new TriggerData(fa.name + "_nFozzy", Data.Center.X, Data.Center.Y);
					var poly = fa.GetEnclosingPolygon(23, 12);
					data.DragPoints = new Engine.Math.DragPointData[poly.Count];
					data.IsLocked = true;

					for (int i = 0; i < poly.Count; i++)
					{
						// Poly points are expressed in flipper's frame: transpose to Table's frame as this is the basis uses for drag points
						var p = table.transform.InverseTransformPoint(fa.transform.TransformPoint(poly[i]));
						data.DragPoints[i] = new Engine.Math.DragPointData(p.x, p.y);
					}
					var triggerAuth = nFozzyCorrection.AddComponent<TriggerAuthoring>();
					Trigger trigger = new Trigger(data);
					triggerAuth.SetItem(trigger);

					var triggerColl = nFozzyCorrection.AddComponent<TriggerColliderAuthoring>();
					triggerColl.Data.HitHeight = 150F; // nFozzy's recommandation, but I think 50 should be ok
					triggerColl.ItemDataChanged();

					nFozzyCorrection.AddComponent<ConvertToEntity>();

					// Register to Table
					if (table.Table != null)
						table.Table.Add<Trigger>(trigger);

					Undo.RegisterCreatedObjectUndo(nFozzyCorrection, "Create nFozzy's corrections object");
				}
			}

			base.OnInspectorGUI();
		}
	}
}
