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

using UnityEditor;
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(HitTargetAuthoring))]
	public class HitTargetInspector : ItemMainInspector<HitTarget, HitTargetData, HitTargetAuthoring>
	{
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutMesh;
		private bool _foldoutPhysics;
		private bool _foldoutMisc;

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			ItemDataField("Position", ref Data.Position);
			ItemDataField("Scale", ref Data.Size);
			ItemDataField("Orientation", ref Data.RotZ);

			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Behavior")) {

				ItemDataField("Drop Speed", ref Data.DropSpeed, false);
				ItemDataField("Raise Delay", ref Data.RaiseDelay, false);
				ItemDataField("Depth Bias", ref Data.DepthBias, false);
				ItemDataField("Visible", ref Data.IsVisible);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			// mesh
			if (_foldoutMesh = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMesh, "Mesh")) {
				DropDownField("Type", ref Data.TargetType, HitTargetMeshInspector.TargetTypeLabels, HitTargetMeshInspector.TargetTypeValues);
				TextureField("Image", ref Data.Image);
				MaterialField("Material", ref Data.Material);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			// physics
			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Has Hit Event", ref Data.UseHitEvent, false);
				ItemDataField("Hit Threshold", ref Data.Threshold, false);

				EditorGUI.BeginDisabledGroup(Data.OverwritePhysics);
				MaterialField("Physics Material", ref Data.PhysicsMaterial, false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Overwrite Material Settings", ref Data.OverwritePhysics, false);

				EditorGUI.BeginDisabledGroup(!Data.OverwritePhysics);
				ItemDataField("Elasticity", ref Data.Elasticity, false);
				ItemDataField("Elasticity Falloff", ref Data.ElasticityFalloff, false);
				ItemDataField("Friction", ref Data.Friction, false);
				ItemDataField("Scatter Angle", ref Data.Scatter, false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Legacy Mode", ref Data.IsLegacy, false);
				ItemDataField("Collidable", ref Data.IsCollidable, false);
				ItemDataField("Is Dropped", ref Data.IsDropped, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			// misc
			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref Data.IsTimerEnabled, false);
				ItemDataField("Timer Interval", ref Data.TimerInterval, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
