using System.Collections.Generic;
using System.Linq;
using VisualPinball.Engine.Game;
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

		public HitObject[] GenerateHitObjects(EventProxy events, Table.Table table)
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
				hitObjects.Add(new HitTriangle(rgv3D, ItemType.Rubber));

				hitObjects.AddRange(GenerateHitEdge(mesh, addedEdges, mesh.Indices[i], mesh.Indices[i + 2]));
				hitObjects.AddRange(GenerateHitEdge(mesh, addedEdges, mesh.Indices[i + 2], mesh.Indices[i + 1]));
				hitObjects.AddRange(GenerateHitEdge(mesh, addedEdges, mesh.Indices[i + 1], mesh.Indices[i]));
			}

			// add collision vertices
			foreach (var mv in mesh.Vertices) {
				var v = new Vertex3D(mv.X, mv.Y, mv.Z);
				hitObjects.Add(new HitPoint(v, ItemType.Rubber));
			}
			return hitObjects.Select(obj => SetupHitObject(obj, events, table)).ToArray();
		}

		private HitObject SetupHitObject(HitObject obj, EventProxy events, Table.Table table) {
			obj.ApplyPhysics(_data, table);

			// hard coded threshold for now
			obj.Threshold = 2.0f;
			obj.Obj = events;
			obj.FireEvents = _data.HitEvent;
			return obj;
		}

		private static IEnumerable<HitObject> GenerateHitEdge(Mesh mesh, EdgeSet addedEdges, int i, int j) {
			var v1 = new Vertex3D(mesh.Vertices[i].X, mesh.Vertices[i].Y, mesh.Vertices[i].Z);
			var v2 = new Vertex3D(mesh.Vertices[j].X, mesh.Vertices[j].Y, mesh.Vertices[j].Z);
			return addedEdges.AddHitEdge(i, j, v1, v2, ItemType.Rubber);
		}
	}
}
