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
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(KickerMeshAuthoring))]
	public class KickerMeshInspector : ItemMeshInspector<Kicker, KickerData, KickerAuthoring, KickerMeshAuthoring>
	{
		public static readonly string[] KickerTypeLabels = {
			"Invisible",
			"Cup",
			"Cup 2",
			"Hole",
			"Hole Simple",
			"Gottlieb",
			"Williams",
		};
		public static readonly int[] KickerTypeValues = {
			KickerType.KickerInvisible,
			KickerType.KickerCup,
			KickerType.KickerCup2,
			KickerType.KickerHole,
			KickerType.KickerHoleSimple,
			KickerType.KickerGottlieb,
			KickerType.KickerWilliams,
		};

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			MaterialFieldLegacy("Material", ref Data.Material);
			DropDownField("Display", ref Data.KickerType, KickerTypeLabels, KickerTypeValues);
			ItemDataField("Radius", ref Data.Radius);
			ItemDataField("Orientation", ref Data.Orientation);

			base.OnInspectorGUI();
		}
	}
}
