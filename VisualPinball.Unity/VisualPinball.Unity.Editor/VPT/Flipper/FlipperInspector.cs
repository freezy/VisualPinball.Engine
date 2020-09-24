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
	[CustomEditor(typeof(FlipperAuthoring))]
	public class FlipperInspector : ItemInspector
	{
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		private FlipperAuthoring _flipper;

		protected override void OnEnable()
		{
			base.OnEnable();
			_flipper = (FlipperAuthoring)target;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				TextureField("Image", ref _flipper.data.Image);
				MaterialField("Material", ref _flipper.data.Material);
				MaterialField("Rubber Material", ref _flipper.data.RubberMaterial);
				ItemDataField("Rubber Thickness", ref _flipper.data.RubberThickness);
				ItemDataField("Rubber Offset Height", ref _flipper.data.RubberHeight);
				ItemDataField("Rubber Width", ref _flipper.data.RubberWidth);
				ItemDataField("Visible", ref _flipper.data.IsVisible);
				ItemDataField("Enabled", ref _flipper.data.IsEnabled);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _flipper.data.Center);
				ItemDataField("Base Radius", ref _flipper.data.BaseRadius);
				ItemDataField("End Radius", ref _flipper.data.EndRadius);
				ItemDataField("Length", ref _flipper.data.FlipperRadius);
				ItemDataField("Start Angle", ref _flipper.data.StartAngle);
				ItemDataField("End Angle", ref _flipper.data.EndAngle);
				ItemDataField("Height", ref _flipper.data.Height);
				ItemDataField("Max. Difficulty Length", ref _flipper.data.FlipperRadiusMax);
				SurfaceField("Surface", ref _flipper.data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Mass", ref _flipper.data.Mass, dirtyMesh: false);
				ItemDataField("Strength", ref _flipper.data.Strength, dirtyMesh: false);
				ItemDataField("Elasticity", ref _flipper.data.Elasticity, dirtyMesh: false);
				ItemDataField("Elasticity Falloff", ref _flipper.data.ElasticityFalloff, dirtyMesh: false);
				ItemDataField("Friction", ref _flipper.data.Friction, dirtyMesh: false);
				ItemDataField("Return Strength", ref _flipper.data.Return, dirtyMesh: false);
				ItemDataField("Coil Ramp Up", ref _flipper.data.RampUp, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _flipper.data.Scatter, dirtyMesh: false);
				ItemDataField("EOS Torque", ref _flipper.data.TorqueDamping, dirtyMesh: false);
				ItemDataField("EOS Torque Angle", ref _flipper.data.TorqueDampingAngle, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _flipper.data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _flipper.data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
