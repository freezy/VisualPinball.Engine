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
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Marks the generated spline child of a <see cref="WireRailComponent"/>. The Wire Rail
	/// resolves its spline through this marker rather than by name, and locks the child's
	/// transform because it carries the VPX-to-world conversion the generated geometry relies on.
	/// Unlike drag-point splines, this child holds the authored route itself.
	/// </summary>
	[AddComponentMenu("")]
	[DisallowMultipleComponent]
	public sealed class WireRailSplineComponent : MonoBehaviour
	{
	}
}
