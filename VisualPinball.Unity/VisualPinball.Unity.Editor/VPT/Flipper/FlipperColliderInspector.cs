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
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(FlipperColliderAuthoring))]
	public class FlipperColliderInspector : ItemColliderInspector<Flipper, FlipperData, FlipperAuthoring, FlipperColliderAuthoring>
	{
		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			ItemDataField("Mass", ref Data.Mass, false);
			ItemDataField("Strength", ref Data.Strength, false);
			ItemDataField("Elasticity", ref Data.Elasticity, false);
			ItemDataField("Elasticity Falloff", ref Data.ElasticityFalloff, false);
			ItemDataField("Friction", ref Data.Friction, false);
			ItemDataField("Return Strength", ref Data.Return, false);
			ItemDataField("Coil Ramp Up", ref Data.RampUp, false);
			ItemDataField("Scatter Angle", ref Data.Scatter, false);
			ItemDataField("EOS Torque", ref Data.TorqueDamping, false);
			ItemDataField("EOS Torque Angle", ref Data.TorqueDampingAngle, false);

			base.OnInspectorGUI();
		}
	}
}
