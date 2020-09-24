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

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RubberColliderAuthoring))]
	public class RubberColliderInspector : ItemColliderInspector<Rubber, RubberData, RubberAuthoring, RubberColliderAuthoring>
	{
		private RubberData _rubberData;

		private bool _foldoutMaterial = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_rubberData = Data;
		}

		public override void OnInspectorGUI()
		{
			if (_rubberData == null) {
				NoDataPanel();
				return;
			}

			ItemDataField("Collidable", ref _rubberData.IsCollidable, false);

			EditorGUI.BeginDisabledGroup(!_rubberData.IsCollidable);
			ItemDataField("Has Hit Event", ref _rubberData.HitEvent, false);
			ItemDataField("Hit Height", ref _rubberData.HitHeight, false);

			if (_foldoutMaterial = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMaterial, "Physics Material")) {
				EditorGUI.BeginDisabledGroup(_rubberData.OverwritePhysics);
				MaterialField("Preset", ref _rubberData.PhysicsMaterial, false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Overwrite Preset", ref _rubberData.OverwritePhysics, false);

				EditorGUI.BeginDisabledGroup(!_rubberData.OverwritePhysics);
				ItemDataField("Elasticity", ref _rubberData.Elasticity, false);
				ItemDataField("Elasticity Falloff", ref _rubberData.ElasticityFalloff, false);
				ItemDataField("Friction", ref _rubberData.Friction, false);
				ItemDataField("Scatter Angle", ref _rubberData.Scatter, false);
				EditorGUI.EndDisabledGroup();
			}

			EditorGUI.EndDisabledGroup();
		}
	}
}
