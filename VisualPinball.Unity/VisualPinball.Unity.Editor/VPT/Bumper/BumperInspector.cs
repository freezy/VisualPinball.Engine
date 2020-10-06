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
	[CustomEditor(typeof(BumperAuthoring))]
	public class BumperInspector : ItemInspector
	{
		private BumperAuthoring _bumper;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_bumper = target as BumperAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				ItemDataField("Radius", ref _bumper.Data.Radius);
				ItemDataField("Height Scale", ref _bumper.Data.HeightScale);
				ItemDataField("Orientation", ref _bumper.Data.Orientation);
				ItemDataField("Ring Speed", ref _bumper.Data.RingSpeed, dirtyMesh: false);
				ItemDataField("Ring Drop Offset", ref _bumper.Data.RingDropOffset, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _bumper.Data.Center);
				SurfaceField("Surface", ref _bumper.Data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Has Hit Event", ref _bumper.Data.HitEvent, dirtyMesh: false);
				ItemDataField("Force", ref _bumper.Data.Force, dirtyMesh: false);
				ItemDataField("Hit Threshold", ref _bumper.Data.Threshold, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _bumper.Data.Scatter, dirtyMesh: false);
				ItemDataField("Collidable", ref _bumper.Data.IsCollidable, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _bumper.Data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _bumper.Data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
