// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

namespace VisualPinball.Unity
{
	/// <summary>
	/// Global import context used by runtime components during editor-driven table conversion.
	/// </summary>
	public static class ImportContext
	{
		/// <summary>
		/// If true, game items skip parenting to named VPX surfaces during import.
		/// </summary>
		public static bool SkipSurfaceParenting;

		/// <summary>
		/// If true, visual ramp meshes use collision geometry parameters during import.
		/// </summary>
		public static bool UseColliderGeometryForRampMeshes;
	}
}
