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
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(KickerAuthoring))]
	public class KickerInspector : ItemMainInspector<Kicker, KickerData, KickerAuthoring>
	{
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

			if (_foldoutMesh = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMesh, "Mesh")) {
				MaterialField("Material", ref Data.Material);
				DropDownField("Display", ref Data.KickerType, KickerMeshInspector.KickerTypeLabels, KickerMeshInspector.KickerTypeValues);
				ItemDataField("Radius", ref Data.Radius);
				ItemDataField("Orientation", ref Data.Orientation);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "State & Physics")) {
				ItemDataField("Enabled", ref Data.IsEnabled, false);
				ItemDataField("Fall Through", ref Data.FallThrough, false);
				ItemDataField("Legacy", ref Data.LegacyMode, false);
				ItemDataField("Scatter Angle", ref Data.Scatter, false);
				ItemDataField("Hit Accuracy", ref Data.HitAccuracy, false);
				ItemDataField("Hit Height", ref Data.HitHeight, false);

				ItemDataField("Default Angle", ref Data.Angle, false);
				ItemDataField("Default Speed", ref Data.Speed, false);
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
