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
				MaterialField("Material", ref _kicker.data.Material);
				DropDownField("Display", ref _kicker.data.KickerType, _kickerTypeStrings, _kickerTypeValues);
				ItemDataField("Radius", ref _kicker.data.Radius);
				ItemDataField("Orientation", ref _kicker.data.Orientation);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _kicker.data.Center);
				SurfaceField("Surface", ref _kicker.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "State & Physics")) {
				ItemDataField("Enabled", ref _kicker.data.IsEnabled, dirtyMesh: false);
				ItemDataField("Fall Through", ref _kicker.data.FallThrough, dirtyMesh: false);
				ItemDataField("Legacy", ref _kicker.data.LegacyMode, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _kicker.data.Scatter, dirtyMesh: false);
				ItemDataField("Hit Accuracy", ref _kicker.data.HitAccuracy, dirtyMesh: false);
				ItemDataField("Hit Height", ref _kicker.data.HitHeight, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _kicker.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _kicker.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
