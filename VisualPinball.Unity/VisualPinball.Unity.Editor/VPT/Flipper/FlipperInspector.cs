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
				TextureField("Image", ref _flipper.Data.Image);
				MaterialField("Material", ref _flipper.Data.Material);
				MaterialField("Rubber Material", ref _flipper.Data.RubberMaterial);
				ItemDataField("Rubber Thickness", ref _flipper.Data.RubberThickness, onChanged: _flipper.OnRubberWidthUpdated);
				ItemDataField("Rubber Offset Height", ref _flipper.Data.RubberHeight);
				ItemDataField("Rubber Width", ref _flipper.Data.RubberWidth);
				ItemDataField("Visible", ref _flipper.Data.IsVisible);
				ItemDataField("Enabled", ref _flipper.Data.IsEnabled);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position")) {
				ItemDataField("", ref _flipper.Data.Center);
				ItemDataField("Base Radius", ref _flipper.Data.BaseRadius);
				ItemDataField("End Radius", ref _flipper.Data.EndRadius);
				ItemDataField("Length", ref _flipper.Data.FlipperRadius);
				ItemDataField("Start Angle", ref _flipper.Data.StartAngle);
				ItemDataField("End Angle", ref _flipper.Data.EndAngle);
				ItemDataField("Height", ref _flipper.Data.Height);
				ItemDataField("Max. Difficulty Length", ref _flipper.Data.FlipperRadiusMax);
				SurfaceField("Surface", ref _flipper.Data.Surface);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Mass", ref _flipper.Data.Mass, dirtyMesh: false);
				ItemDataField("Strength", ref _flipper.Data.Strength, dirtyMesh: false);
				ItemDataField("Elasticity", ref _flipper.Data.Elasticity, dirtyMesh: false);
				ItemDataField("Elasticity Falloff", ref _flipper.Data.ElasticityFalloff, dirtyMesh: false);
				ItemDataField("Friction", ref _flipper.Data.Friction, dirtyMesh: false);
				ItemDataField("Return Strength", ref _flipper.Data.Return, dirtyMesh: false);
				ItemDataField("Coil Ramp Up", ref _flipper.Data.RampUp, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _flipper.Data.Scatter, dirtyMesh: false);
				ItemDataField("EOS Torque", ref _flipper.Data.TorqueDamping, dirtyMesh: false);
				ItemDataField("EOS Torque Angle", ref _flipper.Data.TorqueDampingAngle, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _flipper.Data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _flipper.Data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
