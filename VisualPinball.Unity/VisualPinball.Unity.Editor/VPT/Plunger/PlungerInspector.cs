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

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PlungerAuthoring))]
	public class PlungerInspector : ItemInspector
	{
		private PlungerAuthoring _plunger;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutStateAndPhysics = true;
		private bool _foldoutMisc = true;

		private static readonly string[] PlungerTypeStrings = { "Modern", "Flat", "Custom" };
		private static readonly int[] PlungerTypeValues = { PlungerType.PlungerTypeModern, PlungerType.PlungerTypeFlat, PlungerType.PlungerTypeCustom };

		protected override void OnEnable()
		{
			base.OnEnable();
			_plunger = target as PlungerAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DropDownField("Type", ref _plunger.Data.Type, PlungerTypeStrings, PlungerTypeValues);
				MaterialField("Material", ref _plunger.Data.Material);
				TextureField("Image", ref _plunger.Data.Image);
				ItemDataField("Flat Frames", ref _plunger.Data.AnimFrames);
				ItemDataField("Width", ref _plunger.Data.Width);
				ItemDataField("Z Adjustment", ref _plunger.Data.ZAdjust);
				EditorGUILayout.LabelField("Custom Settings");
				EditorGUI.indentLevel++;
				ItemDataField("Rod Diameter", ref _plunger.Data.RodDiam);
				ItemDataField("Tip Shape", ref _plunger.Data.TipShape); // TODO: break this down and provide individual fields
				ItemDataField("Ring Gap", ref _plunger.Data.RingGap);
				ItemDataField("Ring Diam", ref _plunger.Data.RingDiam);
				ItemDataField("Ring Width", ref _plunger.Data.RingWidth);
				ItemDataField("Spring Diam", ref _plunger.Data.SpringDiam);
				ItemDataField("Spring Gauge", ref _plunger.Data.SpringGauge);
				ItemDataField("Spring Loops", ref _plunger.Data.SpringLoops);
				ItemDataField("End Loops", ref _plunger.Data.SpringEndLoops);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _plunger.Data.Center);
				SurfaceField("Surface", ref _plunger.Data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutStateAndPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutStateAndPhysics, "State & Physics")) {
				ItemDataField("Pull Speed", ref _plunger.Data.SpeedPull, dirtyMesh: false);
				ItemDataField("Release Speed", ref _plunger.Data.SpeedFire, dirtyMesh: false);
				ItemDataField("Stroke Length", ref _plunger.Data.Stroke, dirtyMesh: false);
				ItemDataField("Scatter Velocity", ref _plunger.Data.ScatterVelocity, dirtyMesh: false);
				ItemDataField("Enable Mechanical Plunger", ref _plunger.Data.IsMechPlunger, dirtyMesh: false);
				ItemDataField("Auto Plunger", ref _plunger.Data.AutoPlunger, dirtyMesh: false);
				ItemDataField("Visible", ref _plunger.Data.IsVisible);
				ItemDataField("Mech Strength", ref _plunger.Data.MechStrength, dirtyMesh: false);
				ItemDataField("Momentum Xfer", ref _plunger.Data.MomentumXfer, dirtyMesh: false);
				ItemDataField("Park Position (0..1)", ref _plunger.Data.ParkPosition, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _plunger.Data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _plunger.Data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
