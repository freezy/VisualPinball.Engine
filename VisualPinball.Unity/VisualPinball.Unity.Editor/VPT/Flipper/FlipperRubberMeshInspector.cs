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
using VisualPinball.Engine.VPT.Flipper;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(FlipperRubberMeshAuthoring))]
	public class FlipperRubberMeshInspector : ItemMeshInspector<Flipper, FlipperData, FlipperAuthoring, FlipperRubberMeshAuthoring>
	{
		private FlipperData _flipperData;

		private bool _foldoutMaterial = true;
		private bool _foldoutSlingshot = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_flipperData = Data;
		}

		public override void OnInspectorGUI()
		{
			if (_flipperData == null) {
				NoDataPanel();
				return;
			}

			MaterialField("Rubber Material", ref _flipperData.RubberMaterial);
			ItemDataField("Rubber Thickness", ref _flipperData.RubberThickness);
			ItemDataField("Rubber Offset Height", ref _flipperData.RubberHeight);
			ItemDataField("Rubber Width", ref _flipperData.RubberWidth);

			base.OnInspectorGUI();
		}
	}
}
