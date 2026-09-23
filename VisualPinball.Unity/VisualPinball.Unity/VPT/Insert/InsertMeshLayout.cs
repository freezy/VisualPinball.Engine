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
	/// What the normal bake needs to know about a generated insert mesh, beyond
	/// the mesh itself.
	/// </summary>
	public sealed class InsertMeshLayout
	{
		/// <summary>The UV chart each vertex was laid out in.</summary>
		public int[] VertexCharts;

		/// <summary>
		/// The chart baked from a flat copy of itself: the raised middle, starburst
		/// and all, or the floor when there is no channel to raise it.
		/// </summary>
		public int FlatChart;

		/// <summary>
		/// The local height the flat copy lies at, which is where the raised middle
		/// would be without its starburst.
		/// </summary>
		public float FlatHeight;

		/// <summary>
		/// A hash of the generated vertices, normals, UVs and triangles. A bake made
		/// for one mesh fits another exactly when their hashes match, whichever
		/// parameters or generator version produced them.
		/// </summary>
		public Hash128 Hash;
	}
}
