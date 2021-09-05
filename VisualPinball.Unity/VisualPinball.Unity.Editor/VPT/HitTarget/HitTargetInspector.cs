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

using System.Collections.Generic;
using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(HitTargetAuthoring)), CanEditMultipleObjects]
	public class HitTargetInspector : TargetInspector
	{
		protected override string MeshAssetFolder
			=> "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Art/Meshes/Hit Target";
		protected override Dictionary<string, int> MeshTypeMapping => new Dictionary<string, int> {
			{ "Narrow", Engine.VPT.TargetType.HitFatTargetSlim },
			{ "Rectangle Fat Narrow", Engine.VPT.TargetType.HitFatTargetSlim },
			{ "Rectangle Fat", Engine.VPT.TargetType.HitFatTargetRectangle },
			{ "Rectangle", Engine.VPT.TargetType.HitTargetRectangle },
			{ "Round", Engine.VPT.TargetType.HitTargetRound },
			{ "Square Fat", Engine.VPT.TargetType.HitFatTargetSquare },
		};
	}
}
