// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	/// <summary>
	/// Marks a generated drag-point spline GameObject. The object is authoring-only and is
	/// rebuilt from its owner's packed drag points, so the package writer excludes its subtree.
	/// </summary>
	[AddComponentMenu("")]
	[DisallowMultipleComponent]
	public sealed class GeneratedDragPointSplineComponent : MonoBehaviour
	{
	}
}
