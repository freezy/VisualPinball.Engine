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
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PrimitiveColliderAuthoring))]
	public class PrimitiveColliderInspector : ItemColliderInspector<Primitive, PrimitiveData, PrimitiveAuthoring, PrimitiveColliderAuthoring>
	{
		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			EditorGUI.BeginDisabledGroup(Data.IsToy || !Data.IsCollidable);

			ItemDataField("Has Hit Event", ref Data.HitEvent, false);
			EditorGUI.BeginDisabledGroup(!Data.HitEvent);
			ItemDataField("Hit Threshold", ref Data.Threshold, false);
			EditorGUI.EndDisabledGroup();

			EditorGUI.BeginDisabledGroup(Data.OverwritePhysics);
			PhysicsMaterialField("Physics Material", ref ColliderAuthoring.PhysicsMaterial);
			EditorGUI.EndDisabledGroup();
			ItemDataField("Overwrite Material Settings", ref Data.OverwritePhysics, false);
			EditorGUI.BeginDisabledGroup(!Data.OverwritePhysics);
			ItemDataField("Elasticity", ref Data.Elasticity, false);
			ItemDataField("Elasticity Falloff", ref Data.ElasticityFalloff, false);
			ItemDataField("Friction", ref Data.Friction, false);
			ItemDataField("Scatter Angle", ref Data.Scatter, false);
			EditorGUI.EndDisabledGroup();

			EditorGUI.EndDisabledGroup();

			EditorGUI.BeginDisabledGroup(Data.IsToy);
			ItemDataField("Collidable", ref Data.IsCollidable, false);
			EditorGUI.EndDisabledGroup();

			ItemDataField("Toy", ref Data.IsToy, false);

			EditorGUI.BeginDisabledGroup(Data.IsToy);
			ItemDataSlider("Reduce Polygons By", ref Data.CollisionReductionFactor, 0f, 1f, false);
			EditorGUI.EndDisabledGroup();

			base.OnInspectorGUI();
		}
	}
}
