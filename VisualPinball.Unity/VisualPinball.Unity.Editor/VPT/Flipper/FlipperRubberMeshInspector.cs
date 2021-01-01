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
	[CustomEditor(typeof(FlipperRubberMeshAuthoring))]
	public class FlipperRubberMeshInspector : ItemMeshInspector<Flipper, FlipperData, FlipperAuthoring, FlipperRubberMeshAuthoring>
	{
		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			MaterialField("Rubber Material", ref Data.RubberMaterial);
			ItemDataField("Rubber Thickness", ref Data.RubberThickness);
			ItemDataField("Rubber Offset Height", ref Data.RubberHeight);
			ItemDataField("Rubber Width", ref Data.RubberWidth);

			base.OnInspectorGUI();
		}
	}
}
