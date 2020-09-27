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
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(KickerAuthoring))]
	public class KickerInspector : ItemInspector
	{
		private KickerAuthoring _kicker;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		private static string[] _kickerTypeStrings = {
			"Invisible",
			"Cup",
			"Cup 2",
			"Hole",
			"Hole Simple",
			"Gottlieb",
			"Williams",
		};
		private static int[] _kickerTypeValues = {
			KickerType.KickerInvisible,
			KickerType.KickerCup,
			KickerType.KickerCup2,
			KickerType.KickerHole,
			KickerType.KickerHoleSimple,
			KickerType.KickerGottlieb,
			KickerType.KickerWilliams,
		};

		protected override void OnEnable()
		{
			base.OnEnable();
			_kicker = target as KickerAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				MaterialField("Material", ref _kicker.Data.Material);
				DropDownField("Display", ref _kicker.Data.KickerType, _kickerTypeStrings, _kickerTypeValues);
				ItemDataField("Radius", ref _kicker.Data.Radius);
				ItemDataField("Orientation", ref _kicker.Data.Orientation);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _kicker.Data.Center);
				SurfaceField("Surface", ref _kicker.Data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "State & Physics")) {
				ItemDataField("Enabled", ref _kicker.Data.IsEnabled, dirtyMesh: false);
				ItemDataField("Fall Through", ref _kicker.Data.FallThrough, dirtyMesh: false);
				ItemDataField("Legacy", ref _kicker.Data.LegacyMode, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _kicker.Data.Scatter, dirtyMesh: false);
				ItemDataField("Hit Accuracy", ref _kicker.Data.HitAccuracy, dirtyMesh: false);
				ItemDataField("Hit Height", ref _kicker.Data.HitHeight, dirtyMesh: false);

				ItemDataField("Default Angle", ref _kicker.Data.Angle, dirtyMesh: false);
				ItemDataField("Default Speed", ref _kicker.Data.Speed, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _kicker.Data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _kicker.Data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
