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
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SurfaceColliderAuthoring))]
	public class SurfaceColliderInspector : ItemColliderInspector<Surface, SurfaceData, SurfaceAuthoring, SurfaceColliderAuthoring>
	{
		private bool _foldoutMaterial = true;
		private bool _foldoutSlingshot = true;

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			ItemDataField("Has Hit Event", ref Data.HitEvent, false);
			EditorGUI.BeginDisabledGroup(!Data.HitEvent);
			ItemDataField("Hit Threshold", ref Data.Threshold, false);
			EditorGUI.EndDisabledGroup();

			ItemDataField("Can Drop", ref Data.IsDroppable, false);
			ItemDataField("Is Bottom Collidable", ref Data.IsBottomSolid, false);

			if (_foldoutSlingshot = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSlingshot, "Slingshot")) {
				ItemDataField("Slingshot Force", ref Data.SlingshotForce, false);
				ItemDataField("Slingshot Threshold", ref Data.SlingshotThreshold, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMaterial = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMaterial, "Physics Material")) {
				EditorGUI.BeginDisabledGroup(Data.OverwritePhysics);
				MaterialField("Preset", ref Data.PhysicsMaterial, false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Overwrite Preset", ref Data.OverwritePhysics, false);

				EditorGUI.BeginDisabledGroup(!Data.OverwritePhysics);
				ItemDataField("Elasticity", ref Data.Elasticity, false);
				ItemDataField("Friction", ref Data.Friction, false);
				ItemDataField("Scatter Angle", ref Data.Scatter, false);
				EditorGUI.EndDisabledGroup();
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
