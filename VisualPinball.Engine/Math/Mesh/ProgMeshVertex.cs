// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

using System.Diagnostics;
using System.Collections.Generic;

namespace VisualPinball.Engine.Math.Mesh
{
	/// <summary>
	/// For the polygon reduction algorithm we use data structures
	/// that contain a little bit more information than the usual
	/// indexed face set type of data structure.
	/// From a vertex we wish to be able to quickly get the
	/// neighboring faces and vertices.
	/// </summary>
	internal class ProgMeshVertex
	{
		/// <summary>
		/// location of point in euclidean space
		/// </summary>
		public ProgMeshFloat3 Position;

		/// <summary>
		/// place of vertex in original Array
		/// </summary>
		public readonly int ID;

		/// <summary>
		/// adjacent vertices
		/// </summary>
		public readonly List<ProgMeshVertex> Neighbor = new List<ProgMeshVertex>();

		/// <summary>
		/// adjacent triangles
		/// </summary>
		public readonly List<ProgMeshTriangle> Face = new List<ProgMeshTriangle>();

		/// <summary>
		/// cached cost of collapsing edge
		/// </summary>
		public float ObjDist;

		/// <summary>
		/// candidate vertex for collapse
		/// </summary>
		public ProgMeshVertex Collapse;

		public ProgMeshVertex(ProgMeshFloat3 v, int id)
		{
			Position = v;
			ID = id;
		}

		public void Dispose(ProgMesh pm)
		{
			Debug.Assert(Face.Count == 0, "[ProgMeshVertex.destroy] face.size() == 0");
			while (Neighbor.Count > 0) {
				ProgMeshUtil.RemoveFillWithBack(Neighbor[0].Neighbor, this);
				ProgMeshUtil.RemoveFillWithBack(Neighbor, Neighbor[0]);
			}
			ProgMeshUtil.RemoveFillWithBack(pm.Vertices, this);
		}

		public void RemoveIfNonNeighbor(ProgMeshVertex n)
		{
			// removes n from neighbor Array if n isn't a neighbor.
			if (!Neighbor.Contains(n)) {
				return;
			}

			foreach (var face in Face) {
				if (face.HasVertex(n)) {
					return;
				}
			}
			ProgMeshUtil.RemoveFillWithBack(Neighbor, n);
		}
	}
}
