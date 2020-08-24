// ReSharper disable LoopCanBeConvertedToQuery

using System.Collections.Generic;
using System.Linq;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Math.ProgMesh;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Primitivehitp
{
	public class PrimitiveHitGenerator
	{
		private readonly Primitive _primitive;
		private readonly PrimitiveData _data;

		public PrimitiveHitGenerator(Primitive primitive)
		{
			_primitive = primitive;
			_data = primitive.Data;
		}

		public HitObject[] GenerateHitObjects(Table.Table table, Mesh mesh)
		{
			var hitObjects = new List<HitObject>();

			if (_data.Name == "playfield_mesh") {
				_data.IsVisible = false;
				_primitive.UseAsPlayfield = true;
			}

			// playfield can't be a toy
			if (_data.IsToy && !_primitive.UseAsPlayfield) {
				return hitObjects.ToArray();
			}

			//RecalculateMatrices();
			//TransformVertices(); //!! could also only do this for the optional reduced variant!

			var reducedVertices = System.Math.Max(
				(uint) MathF.Pow(mesh.Vertices.Length,
					MathF.Clamp(1f - _data.CollisionReductionFactor, 0f, 1f) * 0.25f + 0.75f), 420u //!! 420 = magic
			);

			if (reducedVertices < mesh.Vertices.Length) {
				mesh = ComputeReducedMesh(mesh, reducedVertices);
			}

			var addedEdges = new EdgeSet();

			// add collision triangles and edges
			for (var i = 0; i < mesh.Indices.Length; ++i) {
				var i0 = mesh.Indices[i];
				var i1 = mesh.Indices[i + 1];
				var i2 = mesh.Indices[i + 2];


				// NB: HitTriangle wants CCW vertices, but for rendering we have them in CW order
				var rgv3D = new[] {
					mesh.Vertices[i0].GetVertex(),
					mesh.Vertices[i2].GetVertex(),
					mesh.Vertices[i1].GetVertex(),
				};

				hitObjects.Add(new HitTriangle(rgv3D, ItemType.Primitive));

				hitObjects.AddRange(addedEdges.AddHitEdge(i0, i1, rgv3D[0], rgv3D[2], ItemType.Primitive));
				hitObjects.AddRange(addedEdges.AddHitEdge(i1, i2, rgv3D[2], rgv3D[1], ItemType.Primitive));
				hitObjects.AddRange(addedEdges.AddHitEdge(i2, i0, rgv3D[1], rgv3D[0], ItemType.Primitive));
			}

			// add collision vertices
			foreach (var vertex in mesh.Vertices) {
				hitObjects.Add(new HitPoint(vertex.GetVertex(), ItemType.Primitive));
			}

			return hitObjects.Select(ho => SetupHitObject(ho, table)).ToArray();
		}

		private static Mesh ComputeReducedMesh(Mesh mesh, uint reducedVertices)
		{
			var progVertices = new List<Vertex3D>(mesh.Vertices.Length);

			//!! opt. use original data directly!
			for (var i = 0; i < mesh.Vertices.Length; ++i) {
				progVertices[i] = new Vertex3D(
					mesh.Vertices[i].X,
					mesh.Vertices[i].Y,
					mesh.Vertices[i].Z
				);
			}

			var progIndices = new List<tridata>(mesh.Indices.Length / 3);
			{
				var i2 = 0;
				for (var i = 0; i < mesh.Indices.Length; i += 3) {
					var t = new tridata(
						mesh.Indices[i],
						mesh.Indices[i + 1],
						mesh.Indices[i + 2]
					);
					if (t.v[0] != t.v[1] && t.v[1] != t.v[2] && t.v[2] != t.v[0]) {
						progIndices[i2++] = t;
					}
				}

				if (i2 < progIndices.Count) {
					progIndices.AddRange(new tridata[progIndices.Count - i2]);
				}
			}
			var progMap = new List<int>();
			var progPerm = new List<int>();
			ProgMesh.ProgressiveMesh(progVertices, progIndices, progMap, progPerm);
			Util.PermuteVertices(progPerm, progVertices, progIndices);
			progPerm.Clear();

			var progNewIndices = new List<tridata>();
			ProgMesh.ReMapIndices(reducedVertices, progIndices, progNewIndices, progMap);
			progIndices.Clear();
			progMap.Clear();

			var reducedIndices = new List<int>();
			foreach (var index in progNewIndices) {
				reducedIndices.Add(index.v[0]);
				reducedIndices.Add(index.v[1]);
				reducedIndices.Add(index.v[2]);
			}

			return new Mesh(
				progVertices.Select(pv => new Vertex3DNoTex2(pv.X, pv.Y, pv.Z)).ToArray(),
				reducedIndices.ToArray()
			);
		}

		private HitObject SetupHitObject(HitObject obj, Table.Table table)
		{
			if (!_primitive.UseAsPlayfield) {
				obj.ApplyPhysics(_data, table);

			} else {
				obj.SetElasticity(table.Data.Elasticity, table.Data.ElasticityFalloff);
				obj.SetFriction(table.Data.Friction);
				obj.SetScatter(MathF.DegToRad(table.Data.Scatter));
				obj.SetEnabled(true);
			}

			obj.Threshold = _data.Threshold;
			obj.E = true;
			obj.FireEvents = _data.HitEvent;
			return obj;
		}
	}
}
