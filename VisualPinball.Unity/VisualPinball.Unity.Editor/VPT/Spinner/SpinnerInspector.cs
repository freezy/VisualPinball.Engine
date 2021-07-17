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
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SpinnerAuthoring))]
	public class SpinnerInspector : ItemMainInspector<Spinner, SpinnerData, SpinnerAuthoring>
	{
		private bool _foldoutGeometry = true;
		private bool _foldoutMesh;
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

			if (_foldoutGeometry = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutGeometry, "Geometry")) {

				ItemDataField("Length", ref Data.Length);
				ItemDataField("Height", ref Data.Height);
				ItemDataField("Rotation", ref Data.Rotation);
				ItemDataField("Angle Max", ref Data.AngleMax, false);
				ItemDataField("Angle Min", ref Data.AngleMin, false);
				ItemDataField("Elasticity", ref Data.Elasticity, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMesh = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMesh, "Mesh")) {
				ItemDataField("Visible", ref Data.IsVisible);
				TextureFieldLegacy("Texture", ref Data.Image);
				MaterialFieldLegacy("Material", ref Data.Material);
				ItemDataField("Show Bracket", ref Data.ShowBracket);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Damping", ref Data.Damping, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref Data.IsTimerEnabled, false);
				ItemDataField("Timer Interval", ref Data.TimerInterval, false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
