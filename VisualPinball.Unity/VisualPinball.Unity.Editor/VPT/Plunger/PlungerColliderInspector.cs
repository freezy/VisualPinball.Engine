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
using VisualPinball.Engine.VPT.Plunger;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(PlungerColliderAuthoring))]
	public class PlungerColliderInspector : ItemColliderInspector<Plunger, PlungerData, PlungerAuthoring, PlungerColliderAuthoring>
	{
		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			ItemDataField("Pull Speed", ref Data.SpeedPull, false);
			ItemDataField("Release Speed", ref Data.SpeedFire, false);
			ItemDataField("Stroke Length", ref Data.Stroke, false);
			ItemDataField("Scatter Velocity", ref Data.ScatterVelocity, false);
			ItemDataField("Enable Mechanical Plunger", ref Data.IsMechPlunger, false);
			ItemDataField("Auto Plunger", ref Data.AutoPlunger, false);
			ItemDataField("Visible", ref Data.IsVisible);
			ItemDataField("Mech Strength", ref Data.MechStrength, false);
			ItemDataField("Momentum Xfer", ref Data.MomentumXfer, false);
			ItemDataField("Park Position (0..1)", ref Data.ParkPosition, false);

			base.OnInspectorGUI();
		}
	}
}
