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

using UnityEngine;
using Material = VisualPinball.Engine.VPT.Material;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Just a wrapper so we can write materials to disk. <p/>
	///
	/// Materials are actually a big deal in VP, authors as well as players
	/// tweak them all the time, so getting those from external assets instead
	/// of writing them into the scene seems a good plan.
	/// </summary>
	///
	/// <remarks>
	/// Note that while we write the entire material, only physics-related
	/// fields are relevant. Rendering-specific fields are converted into
	/// a Unity material at import and not written back.
	/// </remarks>
	public class PhysicsMaterialAsset : ScriptableObject
	{
		public Material Material;
	}
}
