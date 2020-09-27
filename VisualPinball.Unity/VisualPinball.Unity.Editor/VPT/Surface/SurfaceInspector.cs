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

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SurfaceAuthoring))]
	public class SurfaceInspector : DragPointsItemInspector
	{
		private SurfaceAuthoring _targetSurf;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_targetSurf = target as SurfaceAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				ItemDataField("Top Visible", ref _targetSurf.Data.IsTopBottomVisible);
				TextureField("Top Image", ref _targetSurf.Data.Image);
				MaterialField("Top Material", ref _targetSurf.Data.TopMaterial);
				ItemDataField("Side Visible", ref _targetSurf.Data.IsSideVisible);
				TextureField("Side Image", ref _targetSurf.Data.SideImage);
				MaterialField("Side Material", ref _targetSurf.Data.SideMaterial);
				MaterialField("Slingshot Material", ref _targetSurf.Data.SlingShotMaterial);
				ItemDataField("Animate Slingshot", ref _targetSurf.Data.SlingshotAnimation, dirtyMesh: false);
				ItemDataField("Flipbook", ref _targetSurf.Data.IsFlipbook, dirtyMesh: false);
			}

			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("Top Height", ref _targetSurf.Data.HeightTop);
				ItemDataField("Bottom Height", ref _targetSurf.Data.HeightBottom);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _targetSurf.Data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _targetSurf.Data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
