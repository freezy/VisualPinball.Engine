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
using VisualPinball.Engine.VPT.Ramp;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(RampColliderAuthoring))]
	public class RampColliderInspector : ItemColliderInspector<Ramp, RampData, RampAuthoring, RampColliderAuthoring>
	{
		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

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

			base.OnInspectorGUI();
		}
	}
}
