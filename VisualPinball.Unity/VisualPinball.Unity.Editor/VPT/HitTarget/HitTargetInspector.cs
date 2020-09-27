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
	[CustomEditor(typeof(HitTargetAuthoring))]
	public class HitTargetInspector : ItemInspector
	{
		private HitTargetAuthoring _target;
		private bool _foldoutColorsAndFormatting = true;
		private bool _foldoutPosition = true;
		private bool _foldoutPhysics = true;
		private bool _foldoutMisc = true;

		private static string[] _targetTypeStrings = {
			"Drop Target: Beveled",
			"Drop Target: Simple",
			"Drop Target: Flat Simple",
			"Hit Target: Rectangle",
			"Hit Target: Fat Rectangle",
			"Hit Target: Round",
			"Hit Target: Slim",
			"Hit Target: Fat Slim",
			"Hit Target: Fat Square",
		};
		private static int[] _targetTypeValues = {
			TargetType.DropTargetBeveled,
			TargetType.DropTargetSimple,
			TargetType.DropTargetFlatSimple,
			TargetType.HitTargetRectangle,
			TargetType.HitFatTargetRectangle,
			TargetType.HitTargetRound,
			TargetType.HitTargetSlim,
			TargetType.HitFatTargetSlim,
			TargetType.HitFatTargetSquare,
		};

		protected override void OnEnable()
		{
			base.OnEnable();
			_target = target as HitTargetAuthoring;
		}

		public override void OnInspectorGUI()
		{
			OnPreInspectorGUI();

			if (_foldoutColorsAndFormatting = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutColorsAndFormatting, "Colors & Formatting")) {
				DropDownField("Type", ref _target.Data.TargetType, _targetTypeStrings, _targetTypeValues);
				TextureField("Image", ref _target.Data.Image);
				MaterialField("Material", ref _target.Data.Material);
				ItemDataField("Drop Speed", ref _target.Data.DropSpeed, dirtyMesh: false);
				ItemDataField("Raise Delay", ref _target.Data.RaiseDelay, dirtyMesh: false);
				ItemDataField("Depth Bias", ref _target.Data.DepthBias, dirtyMesh: false);
				ItemDataField("Visible", ref _target.Data.IsVisible);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPosition = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPosition, "Position & Translation")) {
				EditorGUILayout.LabelField("Position");
				EditorGUI.indentLevel++;
				ItemDataField("", ref _target.Data.Position);
				EditorGUI.indentLevel--;

				EditorGUILayout.LabelField("Scale");
				EditorGUI.indentLevel++;
				ItemDataField("", ref _target.Data.Size);
				EditorGUI.indentLevel--;

				ItemDataField("Orientation", ref _target.Data.RotZ);

			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutPhysics, "Physics")) {
				ItemDataField("Has Hit Event", ref _target.Data.UseHitEvent, dirtyMesh: false);
				ItemDataField("Hit Threshold", ref _target.Data.Threshold, dirtyMesh: false);

				EditorGUI.BeginDisabledGroup(_target.Data.OverwritePhysics);
				MaterialField("Physics Material", ref _target.Data.PhysicsMaterial, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Overwrite Material Settings", ref _target.Data.OverwritePhysics, dirtyMesh: false);

				EditorGUI.BeginDisabledGroup(!_target.Data.OverwritePhysics);
				ItemDataField("Elasticity", ref _target.Data.Elasticity, dirtyMesh: false);
				ItemDataField("Elasticity Falloff", ref _target.Data.ElasticityFalloff, dirtyMesh: false);
				ItemDataField("Friction", ref _target.Data.Friction, dirtyMesh: false);
				ItemDataField("Scatter Angle", ref _target.Data.Scatter, dirtyMesh: false);
				EditorGUI.EndDisabledGroup();

				ItemDataField("Legacy Mode", ref _target.Data.IsLegacy, dirtyMesh: false);
				ItemDataField("Collidable", ref _target.Data.IsCollidable, dirtyMesh: false);
				ItemDataField("Is Dropped", ref _target.Data.IsDropped, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			if (_foldoutMisc = EditorGUILayout.BeginFoldoutHeaderGroup(_foldoutMisc, "Misc")) {
				ItemDataField("Timer Enabled", ref _target.Data.IsTimerEnabled, dirtyMesh: false);
				ItemDataField("Timer Interval", ref _target.Data.TimerInterval, dirtyMesh: false);
			}
			EditorGUILayout.EndFoldoutHeaderGroup();

			base.OnInspectorGUI();
		}
	}
}
