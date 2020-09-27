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
	[CustomEditor(typeof(RampAuthoring))]
	public class RampInspector : DragPointsItemInspector
	{
		private RampAuthoring _ramp;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		private static string[] _rampTypeStrings = {
			"Flat",
			"1 Wire",
			"2 Wire",
			"3 Wire Left",
			"3 Wire Right",
			"4 Wire",
		};
		private static int[] _rampTypeValues = {
			RampType.RampTypeFlat,
			RampType.RampType1Wire,
			RampType.RampType2Wire,
			RampType.RampType3WireLeft,
			RampType.RampType3WireRight,
			RampType.RampType4Wire,
		};
		private static string[] _rampImageAlignmentStrings = {
			"World",
			"Wrap",
		};
		private static int[] _rampImageAlignmentValues = {
			RampImageAlignment.ImageModeWorld,
			RampImageAlignment.ImageModeWrap,
		};

		protected override void OnEnable()
		{
			base.OnEnable();
			_ramp = target as RampAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DropDownField("Type", ref _ramp.Data.RampType, _rampTypeStrings, _rampTypeValues);
				TextureField("Image", ref _ramp.Data.Image);
				MaterialField("Material", ref _ramp.Data.Material);
				DropDownField("Image Mode", ref _ramp.Data.ImageAlignment, _rampImageAlignmentStrings, _rampImageAlignmentValues);
				ItemDataField("Apply Image To Wall", ref _ramp.Data.ImageWalls);
				ItemDataField("Visible", ref _ramp.Data.IsVisible);
				ItemDataField("Depth Bias", ref _ramp.Data.DepthBias);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("Top Height", ref _ramp.Data.HeightTop);
				ItemDataField("Bottom Height", ref _ramp.Data.HeightBottom);

				EditorGUILayout.Space(10);
				ItemDataField("Top Width", ref _ramp.Data.WidthTop);
				ItemDataField("Bottom Width", ref _ramp.Data.WidthBottom);

				EditorGUILayout.Space(10);
				EditorGUILayout.LabelField("Visible Wall");
				EditorGUI.indentLevel++;
				ItemDataField("Left Wall", ref _ramp.Data.LeftWallHeightVisible);
				ItemDataField("Right Wall", ref _ramp.Data.RightWallHeightVisible);
				EditorGUI.indentLevel--;
				EditorGUILayout.LabelField("Wire Ramp");
				EditorGUI.indentLevel++;
				ItemDataField("Diameter", ref _ramp.Data.WireDiameter);
				ItemDataField("Distance X", ref _ramp.Data.WireDistanceX);
				ItemDataField("Distance Y", ref _ramp.Data.WireDistanceY);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Has Hit Event", ref _ramp.Data.HitEvent, dirtyMesh: false);
				ItemDataField("Hit Threshold", ref _ramp.Data.Threshold, dirtyMesh: false);

				EditorGUILayout.LabelField("Physical Wall");
				EditorGUI.indentLevel++;
				ItemDataField("Left Wall", ref _ramp.Data.LeftWallHeight);
				ItemDataField("Right Wall", ref _ramp.Data.RightWallHeight);
				EditorGUI.indentLevel--;

				EditorGUI.BeginDisabledGroup(_ramp.Data.OverwritePhysics);
				MaterialField("Physics Material", ref _ramp.Data.PhysicsMaterial, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Overwrite Material Settings", ref _ramp.Data.OverwritePhysics, dirtyMesh: false);

				EditorGUI.BeginDisabledGroup(!_ramp.Data.OverwritePhysics);
				ItemDataField("Elasticity", ref _ramp.Data.Elasticity, dirtyMesh: false);
				ItemDataField("Friction", ref _ramp.Data.Friction, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _ramp.Data.Scatter, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Collidable", ref _ramp.Data.IsCollidable, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _ramp.Data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _ramp.Data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
