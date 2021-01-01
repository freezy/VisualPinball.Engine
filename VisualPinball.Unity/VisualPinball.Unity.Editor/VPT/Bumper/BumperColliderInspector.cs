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
using VisualPinball.Engine.VPT.Bumper;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(BumperColliderAuthoring))]
	public class BumperColliderInspector : ItemColliderInspector<Bumper, BumperData, BumperAuthoring, BumperColliderAuthoring>
	{

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			ItemDataField("Has Hit Event", ref Data.HitEvent, false);
			ItemDataField("Force", ref Data.Force, false);
			ItemDataField("Hit Threshold", ref Data.Threshold, false);
			ItemDataField("Scatter Angle", ref Data.Scatter, false);

			base.OnInspectorGUI();
		}
	}
}
