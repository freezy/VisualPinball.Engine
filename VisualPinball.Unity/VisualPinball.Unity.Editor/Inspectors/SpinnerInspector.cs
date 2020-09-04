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

using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(SpinnerAuthoring))]
	public class SpinnerInspector : ItemInspector
	{
		private SpinnerAuthoring _spinner;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_spinner = target as SpinnerAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				ItemDataField("Visible", ref _spinner.data.IsVisible);
				TextureField("Image", ref _spinner.data.Image);
				MaterialField("Material", ref _spinner.data.Material);
				ItemDataField("Show Bracket", ref _spinner.data.ShowBracket);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _spinner.data.Center);
				ItemDataField("Length", ref _spinner.data.Length);
				ItemDataField("Height", ref _spinner.data.Height);
				ItemDataField("Rotation", ref _spinner.data.Rotation);
				ItemDataField("Angle Max", ref _spinner.data.AngleMax, dirtyMesh: false);
				ItemDataField("Angle Min", ref _spinner.data.AngleMin, dirtyMesh: false);
				ItemDataField("Elasticity", ref _spinner.data.Elasticity, dirtyMesh: false);
				SurfaceField("Surface", ref _spinner.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Damping", ref _spinner.data.Damping, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _spinner.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _spinner.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
