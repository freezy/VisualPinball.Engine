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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Ramp;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RampAuthoring))]
	public class RampInspector : DragPointsItemInspector<Ramp, RampData, RampAuthoring>
	{
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutGeometry = true;
		private bool _foldoutPhysics;
		private bool _foldoutMisc;

		private static readonly string[] RampTypeLabels = {
			"Flat",
			"1 Wire",
			"2 Wire",
			"3 Wire Left",
			"3 Wire Right",
			"4 Wire",
		};
		private static readonly int[] RampTypeValues = {
			RampType.RampTypeFlat,
			RampType.RampType1Wire,
			RampType.RampType2Wire,
			RampType.RampType3WireLeft,
			RampType.RampType3WireRight,
			RampType.RampType4Wire,
		};
		private static readonly string[] RampImageAlignmentLabels = {
			"World",
			"Wrap",
		};
		private static readonly int[] RampImageAlignmentValues = {
			RampImageAlignment.ImageModeWorld,
			RampImageAlignment.ImageModeWrap,
		};

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DropDownField("Type", ref Data.RampType, RampTypeLabels, RampTypeValues, onChanged: ItemAuthoring.UpdateMeshComponents);
				TextureField("Image", ref Data.Image);
				MaterialField("Material", ref Data.Material);
				DropDownField("Image Mode", ref Data.ImageAlignment, RampImageAlignmentLabels, RampImageAlignmentValues);
				ItemDataField("Apply Image To Wall", ref Data.ImageWalls);
				ItemDataField("Depth Bias", ref Data.DepthBias);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutGeometry = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutGeometry, "Geometry")) {
				ItemDataField("Top Height", ref Data.HeightTop);
				ItemDataField("Bottom Height", ref Data.HeightBottom);

				EditorGUILayout.Space(10);
				ItemDataField("Top Width", ref Data.WidthTop);
				ItemDataField("Bottom Width", ref Data.WidthBottom);

				EditorGUILayout.Space(10);
				EditorGUILayout.LabelField("Visible Wall");
				EditorGUI.indentLevel++;
				ItemDataField("Left Wall", ref Data.LeftWallHeightVisible);
				ItemDataField("Right Wall", ref Data.RightWallHeightVisible);
				EditorGUI.indentLevel--;
				EditorGUILayout.LabelField("Wire Ramp");
				EditorGUI.indentLevel++;
				ItemDataField("Diameter", ref Data.WireDiameter);
				ItemDataField("Distance X", ref Data.WireDistanceX);
				ItemDataField("Distance Y", ref Data.WireDistanceY);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Has Hit Event", ref Data.HitEvent, false);
				ItemDataField("Hit Threshold", ref Data.Threshold, false);

				EditorGUILayout.LabelField("Physical Wall");
				EditorGUI.indentLevel++;
				ItemDataField("Left Wall", ref Data.LeftWallHeight);
				ItemDataField("Right Wall", ref Data.RightWallHeight);
				EditorGUI.indentLevel--;

				EditorGUI.BeginDisabledGroup(Data.OverwritePhysics);
				MaterialField("Physics Material", ref Data.PhysicsMaterial, false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Overwrite Material Settings", ref Data.OverwritePhysics, false);

				EditorGUI.BeginDisabledGroup(!Data.OverwritePhysics);
				ItemDataField("Elasticity", ref Data.Elasticity, false);
				ItemDataField("Friction", ref Data.Friction, false);
				ItemDataField("Scatter Angle", ref Data.Scatter, false);
				EditorGUI.EndDisabledGroup();
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
