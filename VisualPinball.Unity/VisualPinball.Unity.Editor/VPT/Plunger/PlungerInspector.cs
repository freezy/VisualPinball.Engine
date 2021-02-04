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
using UnityEngine.InputSystem;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PlungerAuthoring))]
	public class PlungerInspector : ItemMainInspector<Plunger, PlungerData, PlungerAuthoring>
	{
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutStateAndPhysics;
		private bool _foldoutMisc;

		private static readonly string[] PlungerTypeLabels = { "Modern", "Flat", "Custom" };
		private static readonly int[] PlungerTypeValues = { PlungerType.PlungerTypeModern, PlungerType.PlungerTypeFlat, PlungerType.PlungerTypeCustom };

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			ItemDataField("Position", ref Data.Center);
			SurfaceField("Surface", ref Data.Surface);

			OnPreInspectorGUI();

			ItemDataField("Mechanical Plunger", ref Data.IsMechPlunger, false);
			EditorGUI.BeginDisabledGroup(!Data.IsMechPlunger);
			ItemAuthoring.analogPlungerAction = (InputActionReference)EditorGUILayout.ObjectField("Analog Key", ItemAuthoring.analogPlungerAction, typeof(InputActionReference), false);
			EditorGUI.EndDisabledGroup();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DropDownField("Type", ref Data.Type, PlungerTypeLabels, PlungerTypeValues, onChanged: ItemAuthoring.OnTypeChanged);
				MaterialField("Material", ref Data.Material);
				TextureField("Image", ref Data.Image);
				ItemDataField("Flat Frames", ref Data.AnimFrames);
				ItemDataField("Width", ref Data.Width);
				ItemDataField("Z Adjustment", ref Data.ZAdjust);
				EditorGUILayout.LabelField("Custom Settings");
				EditorGUI.indentLevel++;
				ItemDataField("Rod Diameter", ref Data.RodDiam);
				ItemDataField("Tip Shape", ref Data.TipShape); // TODO: break this down and provide individual fields
				ItemDataField("Ring Gap", ref Data.RingGap);
				ItemDataField("Ring Diam", ref Data.RingDiam);
				ItemDataField("Ring Width", ref Data.RingWidth);
				ItemDataField("Spring Diam", ref Data.SpringDiam);
				ItemDataField("Spring Gauge", ref Data.SpringGauge);
				ItemDataField("Spring Loops", ref Data.SpringLoops);
				ItemDataField("End Loops", ref Data.SpringEndLoops);
				EditorGUI.indentLevel--;
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutStateAndPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutStateAndPhysics, "State & Physics")) {
				ItemDataField("Pull Speed", ref Data.SpeedPull, false);
				ItemDataField("Release Speed", ref Data.SpeedFire, false);
				ItemDataField("Stroke Length", ref Data.Stroke, false);
				ItemDataField("Scatter Velocity", ref Data.ScatterVelocity, false);
				ItemDataField("Enable Mechanical Plunger", ref Data.IsMechPlunger, false);
				ItemDataField("Auto Plunger", ref Data.AutoPlunger, false);
				ItemDataField("Mech Strength", ref Data.MechStrength, false);
				ItemDataField("Momentum Xfer", ref Data.MomentumXfer, false);
				ItemDataField("Park Position (0..1)", ref Data.ParkPosition, false);
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
