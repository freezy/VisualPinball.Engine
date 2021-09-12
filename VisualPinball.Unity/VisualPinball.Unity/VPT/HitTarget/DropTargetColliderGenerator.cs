using System.Collections.Generic;
using Unity.Mathematics;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity
{
	public class DropTargetColliderGenerator : TargetColliderGenerator
	{
		public DropTargetColliderGenerator(IApiColliderGenerator api, ITargetData data, IMeshGenerator meshProvider) : base(api, data, meshProvider)
		{
		}

		internal void GenerateColliders(float playfieldHeight, ICollection<ICollider> colliders)
		{
			var localToPlayfield = MeshGenerator.GetTransformationMatrix();
			var hitMesh = MeshGenerator.GetMesh();
			for (var i = 0; i < hitMesh.Vertices.Length; i++) {
				hitMesh.Vertices[i].MultiplyMatrix(localToPlayfield);
			}
			var addedEdges = EdgeSet.Get();
			GenerateCollidables(hitMesh, addedEdges, Data.IsLegacy, colliders);

			var tempMatrix = new Matrix3D().RotateZMatrix(MathF.DegToRad(Data.RotZ));
			var fullMatrix = new Matrix3D().Multiply(tempMatrix);

			if (!Data.IsLegacy) {

				var rgv3D = new Vertex3D[DropTargetHitPlaneVertices.Length];
				var hitShapeOffset = 0.18f;
				if (Data.TargetType == TargetType.DropTargetBeveled) {
					hitShapeOffset = 0.25f;
				}
				if (Data.TargetType == TargetType.DropTargetFlatSimple) {
					hitShapeOffset = 0.13f;
				}

				// now create a special hit shape with hit event enabled to prevent a hit event when hit from behind
				for (var i = 0; i < DropTargetHitPlaneVertices.Length; i++) {
					var dropTargetHitPlaneVertex = DropTargetHitPlaneVertices[i];
					var vert = new Vertex3D(
						dropTargetHitPlaneVertex.x,
						dropTargetHitPlaneVertex.y + hitShapeOffset,
						dropTargetHitPlaneVertex.z
					);

					vert.X *= Data.ScaleX;
					vert.Y *= Data.ScaleY;
					vert.Z *= Data.ScaleZ;
					vert = vert.MultiplyMatrix(fullMatrix);
					rgv3D[i] = new Vertex3D(
						vert.X + Data.PositionX,
						vert.Y + Data.PositionY,
						vert.Z + Data.PositionZ + playfieldHeight
					);
				}

				for (var i = 0; i < DropTargetHitPlaneIndices.Length; i += 3) {
					var i0 = DropTargetHitPlaneIndices[i];
					var i1 = DropTargetHitPlaneIndices[i + 1];
					var i2 = DropTargetHitPlaneIndices[i + 2];

					// NB: HitTriangle wants CCW vertices, but for rendering we have them in CW order
					var rgv0 = rgv3D[i0].ToUnityFloat3();
					var rgv1 = rgv3D[i1].ToUnityFloat3();
					var rgv2 = rgv3D[i2].ToUnityFloat3();

					colliders.Add(new TriangleCollider(rgv0, rgv2, rgv1, GetColliderInfo(true)));

					if (addedEdges.ShouldAddHitEdge(i0, i1)) {
						colliders.Add(new Line3DCollider(rgv0, rgv2, GetColliderInfo(true)));
					}
					if (addedEdges.ShouldAddHitEdge(i1, i2)) {
						colliders.Add(new Line3DCollider(rgv2, rgv1, GetColliderInfo(true)));
					}
					if (addedEdges.ShouldAddHitEdge(i2, i0)) {
						colliders.Add(new Line3DCollider(rgv1, rgv0, GetColliderInfo(true)));
					}
				}

				// add collision vertices
				for (var i = 0; i < DropTargetHitPlaneVertices.Length; ++i) {
					colliders.Add(new PointCollider(rgv3D[i].ToUnityFloat3(), GetColliderInfo(true)));
				}
			}
		}

		private static readonly float3[] DropTargetHitPlaneVertices = {
			new float3(-0.300000f, 0.001737f, -0.160074f),
			new float3(-0.300000f, 0.001738f, 0.439926f),
			new float3(0.300000f, 0.001738f, 0.439926f),
			new float3(0.300000f, 0.001737f, -0.160074f),
			new float3(-0.500000f, 0.001738f, 0.439926f),
			new float3(-0.500000f, 0.001738f, 1.789926f),
			new float3(0.500000f, 0.001738f, 1.789926f),
			new float3(0.500000f, 0.001738f, 0.439926f),
			new float3(-0.535355f, 0.001738f, 0.454570f),
			new float3(-0.535355f, 0.001738f, 1.775281f),
			new float3(-0.550000f, 0.001738f, 0.489926f),
			new float3(-0.550000f, 0.001738f, 1.739926f),
			new float3(0.535355f, 0.001738f, 0.454570f),
			new float3(0.535355f, 0.001738f, 1.775281f),
			new float3(0.550000f, 0.001738f, 0.489926f),
			new float3(0.550000f, 0.001738f, 1.739926f)
		};

		private static readonly int[] DropTargetHitPlaneIndices = {
			0, 1, 2, 2, 3, 0, 1, 4, 5, 6, 7, 2, 5, 6, 1,
			2, 1, 6, 4, 8, 9, 9, 5, 4, 8, 10, 11, 11, 9, 8,
			6, 12, 7, 12, 6, 13, 12, 13, 14, 13, 15, 14,
		};
	}
}
