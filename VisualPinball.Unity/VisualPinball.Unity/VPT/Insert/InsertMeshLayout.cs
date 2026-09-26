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
		/// and all, or -1 when there is no channel to raise it.
		/// </summary>
		public int FlatChart;

		/// <summary>
		/// The triangles of the raised middle's chart, by triangle index, or
		/// empty when there is no raised middle.
		/// </summary>
		public int[] FlatTriangles;

		/// <summary>
		/// Local-space line pairs for the editor's geometry preview. These follow
		/// generated wall quads and surface strips, without triangulation diagonals.
		/// They do not affect the bake hash or runtime mesh.
		/// </summary>
		public Vector3[] PreviewEdges;

		/// <summary>
		/// Whether the raised middle's chart keeps the generator's projection from
		/// above through the bake, instead of being unwrapped, so that any
		/// triangulation of the middle carries the map. Set when the middle holds
		/// an engraved line, whose trough is geometry for the bake alone.
		/// </summary>
		public bool FlatProjected;

		/// <summary>
		/// A plain, flat triangulation of the raised middle without its engraved
		/// line, to stand in for the middle's triangles once a bake fits, or null
		/// when there is no line.
		/// </summary>
		public InsertFlatIsland FlatIsland;

		/// <summary>
		/// The charts of the walls, the outer one with its bottom bevel included,
		/// which matter less than the floors and are given fewer texels.
		/// </summary>
		public int[] WallCharts;

		/// <summary>
		/// The local height the flat copy lies at, which is where the raised middle
		/// is without its starburst.
		/// </summary>
		public float FlatHeight;

		/// <summary>Actual local centre X/Z, sector count and lowest height of the generated starburst, or zero without one.</summary>
		public Vector4 OpticalStarburst;

		/// <summary>
		/// A hash of the generated vertices, normals, UVs and triangles. A bake made
		/// for one mesh fits another exactly when their hashes match, whichever
		/// parameters or generator version produced them.
		/// </summary>
		public Hash128 Hash;
	}

	/// <summary>The raised middle as a plain flat surface, in local space.</summary>
	public sealed class InsertFlatIsland
	{
		public Vector3[] Vertices;
		public int[] Triangles;
	}
}
