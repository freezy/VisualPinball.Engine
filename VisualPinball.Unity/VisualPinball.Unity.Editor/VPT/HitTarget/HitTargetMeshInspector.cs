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
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(HitTargetMeshAuthoring))]
	public class HitTargetMeshInspector : ItemMeshInspector<HitTarget, HitTargetData, HitTargetAuthoring, HitTargetMeshAuthoring>
	{
		public static readonly string[] TargetTypeLabels = {
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

		public static readonly int[] TargetTypeValues = {
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

		public override void OnInspectorGUI()
		{
			if (HasErrors()) {
				return;
			}

			DropDownField("Type", ref Data.TargetType, TargetTypeLabels, TargetTypeValues);
			TextureFieldLegacy("Texture", ref Data.Image);
			MaterialFieldLegacy("Material", ref Data.Material);

			base.OnInspectorGUI();
		}
	}
}
