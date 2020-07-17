using System.Collections.Generic;
using System.Linq;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.HitTarget
{
	public class HitTargetHitGenerator
	{
		private readonly HitTargetData _data;
		private readonly HitTargetMeshGenerator _meshGenerator;

		public HitTargetHitGenerator(HitTargetData data, HitTargetMeshGenerator meshGenerator)
		{
			_data = data;
			_meshGenerator = meshGenerator;
		}

		public HitObject[] GenerateHitObjects(Table.Table table)
		{
			return _data.IsDropTarget
				? GenerateDropTargetHits(table)
				: GenerateHitTargetHits(table);
		}

		private HitObject[] GenerateDropTargetHits(Table.Table table)
		{
			var addedEdges = new EdgeSet();
			var hitMesh = _meshGenerator.GetRenderObjects(table, Origin.Original, false).RenderObjects[0].Mesh;
			return GenerateCollidables(hitMesh, addedEdges, true, table);
		}

		private HitObject[] GenerateHitTargetHits(Table.Table table)
		{
			var addedEdges = new EdgeSet();
			var hitMesh = _meshGenerator.GetRenderObjects(table, Origin.Original, false).RenderObjects[0].Mesh;
			var hitObjects = GenerateCollidables(hitMesh, addedEdges, _data.IsLegacy, table).ToList();

			var tempMatrix = new Matrix3D().RotateZMatrix(MathF.DegToRad(_data.RotZ));
			var fullMatrix = new Matrix3D().Multiply(tempMatrix);

			if (!_data.IsLegacy) {

				var rgv3D = new Vertex3D[DropTargetHitPlaneVertices.Length];
				var hitShapeOffset = 0.18f;
				if (_data.TargetType == TargetType.DropTargetBeveled) {
					hitShapeOffset = 0.25f;
				}
				if (_data.TargetType == TargetType.DropTargetFlatSimple) {
					hitShapeOffset = 0.13f;
				}

				// now create a special hit shape with hit event enabled to prevent a hit event when hit from behind
				for (var i = 0; i < DropTargetHitPlaneVertices.Length; i++) {
					var dropTargetHitPlaneVertex = DropTargetHitPlaneVertices[i];
					var vert = new Vertex3D(
						dropTargetHitPlaneVertex.X,
						dropTargetHitPlaneVertex.Y + hitShapeOffset,
						dropTargetHitPlaneVertex.Z
					);

					vert.X *= _data.Size.X;
					vert.Y *= _data.Size.Y;
					vert.Z *= _data.Size.Z;
					vert.MultiplyMatrix(fullMatrix);
					rgv3D[i] = new Vertex3D(
						vert.X + _data.Position.X,
						vert.Y + _data.Position.Y,
						vert.Z * table.GetScaleZ() + _data.Position.Z + table.TableHeight
					);
				}

				for (var i = 0; i < DropTargetHitPlaneIndices.Length; i += 3) {
					var i0 = DropTargetHitPlaneIndices[i];
					var i1 = DropTargetHitPlaneIndices[i + 1];
					var i2 = DropTargetHitPlaneIndices[i + 2];

					// NB: HitTriangle wants CCW vertices, but for rendering we have them in CW order
					var rgv3D2 = new[] { rgv3D[i0], rgv3D[i2], rgv3D[i1] };

					hitObjects.Add(SetupHitObject(new HitTriangle(rgv3D2, ItemType.HitTarget), true, table));
					hitObjects.AddRange(addedEdges.AddHitEdge(i0, i1, rgv3D2[0], rgv3D2[2], ItemType.HitTarget).Select(obj => SetupHitObject(obj, true, table)));
					hitObjects.AddRange(addedEdges.AddHitEdge(i1, i2, rgv3D2[2], rgv3D2[1], ItemType.HitTarget).Select(obj => SetupHitObject(obj, true, table)));
					hitObjects.AddRange(addedEdges.AddHitEdge(i2, i0, rgv3D2[1], rgv3D2[0], ItemType.HitTarget).Select(obj => SetupHitObject(obj, true, table)));
				}

				// add collision vertices
				for (var i = 0; i < DropTargetHitPlaneVertices.Length; ++i) {
					hitObjects.Add(SetupHitObject(new HitPoint(rgv3D[i], ItemType.HitTarget), true, table));
				}
			}
			return hitObjects.ToArray();
		}

		private HitObject[] GenerateCollidables(Mesh hitMesh, EdgeSet addedEdges, bool setHitObject, Table.Table table)  {

			var hitObjects = new List<HitObject>();

			// add the normal drop target as collidable but without hit event
			for (var i = 0; i < hitMesh.Indices.Length; i += 3) {
				var i0 = hitMesh.Indices[i];
				var i1 = hitMesh.Indices[i + 1];
				var i2 = hitMesh.Indices[i + 2];

				// NB: HitTriangle wants CCW vertices, but for rendering we have them in CW order
				var rgv3D = new [] {
					new Vertex3D(hitMesh.Vertices[i0].X, hitMesh.Vertices[i0].Y, hitMesh.Vertices[i0].Z),
					new Vertex3D(hitMesh.Vertices[i2].X, hitMesh.Vertices[i2].Y, hitMesh.Vertices[i2].Z),
					new Vertex3D(hitMesh.Vertices[i1].X, hitMesh.Vertices[i1].Y, hitMesh.Vertices[i1].Z)
				};

				hitObjects.Add(SetupHitObject(new HitTriangle(rgv3D, ItemType.HitTarget), setHitObject, table));
				hitObjects.AddRange(addedEdges.AddHitEdge(i0, i1, rgv3D[0], rgv3D[2], ItemType.HitTarget).Select(obj => SetupHitObject(obj, setHitObject, table)));
				hitObjects.AddRange(addedEdges.AddHitEdge(i1, i2, rgv3D[2], rgv3D[1], ItemType.HitTarget).Select(obj => SetupHitObject(obj, setHitObject, table)));
				hitObjects.AddRange(addedEdges.AddHitEdge(i2, i0, rgv3D[1], rgv3D[0], ItemType.HitTarget).Select(obj => SetupHitObject(obj, setHitObject, table)));
			}

			// add collision vertices
			foreach (var vertex in hitMesh.Vertices) {
				hitObjects.Add(SetupHitObject(new HitPoint(vertex.GetVertex(), ItemType.HitTarget), setHitObject, table));
			}

			return hitObjects.ToArray();
		}

		private HitObject SetupHitObject(HitObject obj, bool setHitObject, Table.Table table) {
			obj.ApplyPhysics(_data, table);
			obj.Threshold = _data.Threshold;
			obj.FireEvents = setHitObject && _data.UseHitEvent;
			return obj;
		}

		private static readonly Vertex3D[] DropTargetHitPlaneVertices = {
			new Vertex3D(-0.300000f, 0.001737f, -0.160074f),
			new Vertex3D(-0.300000f, 0.001738f, 0.439926f),
			new Vertex3D(0.300000f, 0.001738f, 0.439926f),
			new Vertex3D(0.300000f, 0.001737f, -0.160074f),
			new Vertex3D(-0.500000f, 0.001738f, 0.439926f),
			new Vertex3D(-0.500000f, 0.001738f, 1.789926f),
			new Vertex3D(0.500000f, 0.001738f, 1.789926f),
			new Vertex3D(0.500000f, 0.001738f, 0.439926f),
			new Vertex3D(-0.535355f, 0.001738f, 0.454570f),
			new Vertex3D(-0.535355f, 0.001738f, 1.775281f),
			new Vertex3D(-0.550000f, 0.001738f, 0.489926f),
			new Vertex3D(-0.550000f, 0.001738f, 1.739926f),
			new Vertex3D(0.535355f, 0.001738f, 0.454570f),
			new Vertex3D(0.535355f, 0.001738f, 1.775281f),
			new Vertex3D(0.550000f, 0.001738f, 0.489926f),
			new Vertex3D(0.550000f, 0.001738f, 1.739926f)
		};

		private static readonly int[] DropTargetHitPlaneIndices = {
			0, 1, 2, 2, 3, 0, 1, 4, 5, 6, 7, 2, 5, 6, 1,
			2, 1, 6, 4, 8, 9, 9, 5, 4, 8, 10, 11, 11, 9, 8,
			6, 12, 7, 12, 6, 13, 12, 13, 14, 13, 15, 14,
		};

	}
}
