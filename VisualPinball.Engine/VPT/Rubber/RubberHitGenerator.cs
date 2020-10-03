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

using System.Collections.Generic;
using System.Linq;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Rubber
{
	public class RubberHitGenerator
	{
		private readonly RubberData _data;
		private readonly RubberMeshGenerator _meshGenerator;

		public RubberHitGenerator(RubberData data, RubberMeshGenerator meshGenerator)
		{
			_data = data;
			_meshGenerator = meshGenerator;
		}

		public HitObject[] GenerateHitObjects(Table.Table table, IItem item)
		{
			var hitObjects = new List<HitObject>();
			var addedEdges = new EdgeSet();
			var mesh = _meshGenerator.GetMesh(table, 6, true); //!! adapt hacky code in the function if changing the "6" here

			// add collision triangles and edges
			for (var i = 0; i < mesh.Indices.Length; i += 3) {
				var rgv3D = new Vertex3D[3];
				// NB: HitTriangle wants CCW vertices, but for rendering we have them in CW order
				var v = mesh.Vertices[mesh.Indices[i]];
				rgv3D[0] = new Vertex3D(v.X, v.Y, v.Z);
				v = mesh.Vertices[mesh.Indices[i + 2]];
				rgv3D[1] = new Vertex3D(v.X, v.Y, v.Z);
				v = mesh.Vertices[mesh.Indices[i + 1]];
				rgv3D[2] = new Vertex3D(v.X, v.Y, v.Z);
				hitObjects.Add(new HitTriangle(rgv3D, ItemType.Rubber, item));

				hitObjects.AddRange(GenerateHitEdge(mesh, addedEdges, mesh.Indices[i], mesh.Indices[i + 2], item));
				hitObjects.AddRange(GenerateHitEdge(mesh, addedEdges, mesh.Indices[i + 2], mesh.Indices[i + 1], item));
				hitObjects.AddRange(GenerateHitEdge(mesh, addedEdges, mesh.Indices[i + 1], mesh.Indices[i], item));
			}

			// add collision vertices
			foreach (var mv in mesh.Vertices) {
				var v = new Vertex3D(mv.X, mv.Y, mv.Z);
				hitObjects.Add(new HitPoint(v, ItemType.Rubber, item));
			}
			return hitObjects.Select(obj => SetupHitObject(obj, table)).ToArray();
		}

		private HitObject SetupHitObject(HitObject obj, Table.Table table) {
			obj.ApplyPhysics(_data, table);

			// hard coded threshold for now
			obj.Threshold = 2.0f;
			obj.FireEvents = _data.HitEvent;
			return obj;
		}

		private static IEnumerable<HitObject> GenerateHitEdge(Mesh mesh, EdgeSet addedEdges, int i, int j, IItem item) {
			var v1 = new Vertex3D(mesh.Vertices[i].X, mesh.Vertices[i].Y, mesh.Vertices[i].Z);
			var v2 = new Vertex3D(mesh.Vertices[j].X, mesh.Vertices[j].Y, mesh.Vertices[j].Z);
			return addedEdges.AddHitEdge(i, j, v1, v2, ItemType.Rubber, item);
		}
	}
}
