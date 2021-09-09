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
	[CustomEditor(typeof(DropTargetComponent)), CanEditMultipleObjects]
	public class DropTargetInspector : TargetInspector
	{
		protected override string MeshAssetFolder
			=> "Packages/org.visualpinball.engine.unity/VisualPinball.Unity/Assets/Art/Meshes/Drop Target";
		protected override Dictionary<string, int> MeshTypeMapping => new Dictionary<string, int> {
			{ "Beveled", Engine.VPT.TargetType.DropTargetBeveled },
			{ "Simple Flat", Engine.VPT.TargetType.DropTargetFlatSimple },
			{ "Simple", Engine.VPT.TargetType.DropTargetSimple },
		};
	}
}
