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
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(GateColliderAuthoring))]
	public class GateColliderInspector : ItemColliderInspector<Gate, GateData, GateAuthoring, GateColliderAuthoring>
	{
		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			ItemDataField("Elasticity", ref Data.Elasticity, false);
			ItemDataField("Friction", ref Data.Friction, false);
			ItemDataField("Damping", ref Data.Damping, false);
			ItemDataField("Gravity Factor", ref Data.GravityFactor, false);
			ItemDataField("Collidable", ref Data.IsCollidable, false);
			ItemDataField(GateInspector.TwoWayLabel, ref Data.TwoWay, false);

			base.OnInspectorGUI();
		}
	}
}
