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
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SurfaceColliderAuthoring))]
	public class SurfaceColliderInspector : ItemColliderInspector<Surface, SurfaceData, SurfaceAuthoring, SurfaceColliderAuthoring>
	{
		private SurfaceData _surfaceData;

		private bool _foldoutMaterial = true;
		private bool _foldoutSlingshot = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_surfaceData = Data;
		}

		public override void OnInspectorGUI()
		{
			if (_surfaceData == null) {
				NoDataPanel();
				return;
			}

			ItemDataField("Collidable", ref _surfaceData.IsCollidable, false);

			EditorGUI.BeginDisabledGroup(!_surfaceData.IsCollidable);

			ItemDataField("Has Hit Event", ref _surfaceData.HitEvent, false);
			EditorGUI.BeginDisabledGroup(!_surfaceData.HitEvent);
			ItemDataField("Hit Threshold", ref _surfaceData.Threshold, false);
			EditorGUI.EndDisabledGroup();

			ItemDataField("Can Drop", ref _surfaceData.IsDroppable, false);
			ItemDataField("Is Bottom Collidable", ref _surfaceData.IsBottomSolid, false);

			if (_foldoutSlingshot = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutSlingshot, "Slingshot")) {
				ItemDataField("Slingshot Force", ref _surfaceData.SlingshotForce, false);
				ItemDataField("Slingshot Threshold", ref _surfaceData.SlingshotThreshold, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMaterial = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMaterial, "Physics Material")) {
				EditorGUI.BeginDisabledGroup(_surfaceData.OverwritePhysics);
				MaterialField("Preset", ref _surfaceData.PhysicsMaterial, false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Overwrite Preset", ref _surfaceData.OverwritePhysics, false);

				EditorGUI.BeginDisabledGroup(!_surfaceData.OverwritePhysics);
				ItemDataField("Elasticity", ref _surfaceData.Elasticity, false);
				ItemDataField("Friction", ref _surfaceData.Friction, false);
				ItemDataField("Scatter Angle", ref _surfaceData.Scatter, false);
				EditorGUI.EndDisabledGroup();
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			EditorGUI.EndDisabledGroup();
		}
	}
}
