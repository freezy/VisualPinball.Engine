// ReSharper disable CompareOfFloatsByEqualityOperator

using System;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Rubber
{
	public class RubberMeshGenerator : MeshGenerator
	{
		public readonly Vertex3D MiddlePoint = new Vertex3D();
		private readonly RubberData _data;

		protected override Vertex3D Position => new Vertex3D(MiddlePoint.X, MiddlePoint.Y, MiddlePoint.Z);
		protected override Vertex3D Scale => Vertex3D.One;
		protected override float RotationZ => MathF.DegToRad(_data.RotZ);

		public RubberMeshGenerator(RubberData data)
		{
			_data = data;
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded = true)
		{
			var mesh = GetMesh(table);
			var (preVertexMatrix, preNormalsMatrix) = GetPreMatrix(table, origin, asRightHanded);
			var postMatrix = GetPostMatrix(table, origin);
			return new RenderObjectGroup(_data.Name, "Rubbers", postMatrix, new RenderObject(
				_data.Name,
				mesh.Transform(preVertexMatrix, preNormalsMatrix),
				new PbrMaterial(table.GetMaterial(_data.Material), table.GetTexture(_data.Image)),
				_data.IsVisible
			));
		}

		protected override Tuple<Matrix3D, Matrix3D> GetTransformationMatrix(Table.Table table)
		{
			var fullMatrix = new Matrix3D();
			var tempMat = new Matrix3D();
			fullMatrix.RotateZMatrix(RotationZ);
			tempMat.RotateYMatrix(MathF.DegToRad(_data.RotY));
			fullMatrix.Multiply(tempMat);
			tempMat.RotateXMatrix(MathF.DegToRad(_data.RotX));
			fullMatrix.Multiply(tempMat);

			var vertMatrix = new Matrix3D();
			tempMat.SetTranslation(-Position.X, -Position.Y, -Position.Z);
			vertMatrix.Multiply(tempMat, fullMatrix);
			tempMat.SetScaling(Scale.X, Scale.Y, Scale.Z * table.GetScaleZ());
			vertMatrix.Multiply(tempMat);
			if (_data.Height == _data.HitHeight) {
				// do not z-scale the hit mesh
				tempMat.SetTranslation(Position.X, Position.Y, _data.Height + table.TableHeight);

			} else {
				tempMat.SetTranslation(Position.X, Position.Y, _data.Height * table.GetScaleZ() + table.TableHeight);
			}

			vertMatrix.Multiply(tempMat);

			return new Tuple<Matrix3D, Matrix3D>(vertMatrix, fullMatrix);
		}

		internal Mesh GetMesh(Table.Table table, int acc = -1, bool createHitShape = false)
		{
			var mesh = new Mesh(_data.Name);
			const int accuracy = (int)(10.0f * 1.2f); // see also above

			var splineAccuracy = acc != -1 ? 4.0f * MathF.Pow(10.0f, (10.0f - PhysicsConstants.HitShapeDetailLevel) * (float) (1.0 / 1.5)) : -1.0f;
			var sv = new SplineVertex(_data.DragPoints, _data.Thickness, table.GetDetailLevel(), (int)splineAccuracy);

			var numRings = sv.VertexCount - 1;
			var numSegments = accuracy;

			var numVertices = numRings * numSegments;
			var numIndices = 6 * numVertices; //m_numVertices*2+2;
			var height = _data.HitHeight + table.TableHeight;

			mesh.Vertices = new Vertex3DNoTex2[numVertices];
			mesh.Indices = new int[numIndices];

			var prevB = new Vertex3D();
			var invNr = 1.0f / numRings;
			var invNs = 1.0f / numSegments;
			var index = 0;
			for (var i = 0; i < numRings; i++) {
				var i2 = i == numRings - 1 ? 0 : i + 1;
				var tangent = new Vertex3D(sv.MiddlePoints[i2].X - sv.MiddlePoints[i].X,
					sv.MiddlePoints[i2].Y - sv.MiddlePoints[i].Y, 0.0f);

				Vertex3D biNormal;
				Vertex3D normal;
				if (i == 0) {
					var up = new Vertex3D(sv.MiddlePoints[i2].X + sv.MiddlePoints[i].X, sv.MiddlePoints[i2].Y + sv.MiddlePoints[i].Y, height * 2.0f);
					normal = new Vertex3D(tangent.Y * up.Z, -tangent.X * up.Z, tangent.X * up.Y - tangent.Y * up.X); // = CrossProduct(tangent, up)
					biNormal = new Vertex3D(tangent.Y * normal.Z, -tangent.X * normal.Z, tangent.X * normal.Y - tangent.Y * normal.X); // = CrossProduct(tangent, normal)

				} else {
					normal = prevB.Clone().Cross(tangent);
					biNormal = tangent.Clone().Cross(normal);
				}

				biNormal.Normalize();
				normal.Normalize();
				prevB = biNormal;
				var u = i * invNr;
				for (var j = 0; j < numSegments; j++) {
					var v = ((float)j + u) * invNs;
					var tmp = Vertex3D.GetRotatedAxis(j * (360.0f * invNs), tangent, normal)
						.MultiplyScalar(_data.Thickness * 0.5f);

					mesh.Vertices[index] = new Vertex3DNoTex2 {
						X = sv.MiddlePoints[i].X + tmp.X,
						Y = sv.MiddlePoints[i].Y + tmp.Y
					};
					if (createHitShape && (j == 0 || j == 3)) {
						//!! hack, adapt if changing detail level for hitshape
						// for a hit shape create a more rectangle mesh and not a smooth one
						tmp.Z *= 0.6f;
					}

					mesh.Vertices[index].Z = height + tmp.Z;
					//texel
					mesh.Vertices[index].Tu = u;
					mesh.Vertices[index].Tv = v;
					index++;
				}
			}

			// calculate faces
			for (var i = 0; i < numRings; i++) {
				for (var j = 0; j < numSegments; j++) {
					var quad = new int[4];
					quad[0] = i * numSegments + j;

					if (j != numSegments - 1) {
						quad[1] = i * numSegments + j + 1;

					} else {
						quad[1] = i * numSegments;
					}

					if (i != numRings - 1) {
						quad[2] = (i + 1) * numSegments + j;
						if (j != numSegments - 1) {
							quad[3] = (i + 1) * numSegments + j + 1;

						} else {
							quad[3] = (i + 1) * numSegments;
						}

					} else {
						quad[2] = j;
						if (j != numSegments - 1) {
							quad[3] = j + 1;

						} else {
							quad[3] = 0;
						}
					}

					mesh.Indices[(i * numSegments + j) * 6] = quad[0];
					mesh.Indices[(i * numSegments + j) * 6 + 1] = quad[1];
					mesh.Indices[(i * numSegments + j) * 6 + 2] = quad[2];
					mesh.Indices[(i * numSegments + j) * 6 + 3] = quad[3];
					mesh.Indices[(i * numSegments + j) * 6 + 4] = quad[2];
					mesh.Indices[(i * numSegments + j) * 6 + 5] = quad[1];
				}
			}

			Mesh.ComputeNormals(mesh.Vertices, numVertices, mesh.Indices, numIndices);

			var maxX = Constants.FloatMin;
			var minX = Constants.FloatMax;
			var maxY = Constants.FloatMin;
			var minY = Constants.FloatMax;
			var maxZ = Constants.FloatMin;
			var minZ = Constants.FloatMax;
			for (var i = 0; i < numVertices; i++) {
				if (maxX < mesh.Vertices[i].X) {
					maxX = mesh.Vertices[i].X;
				}

				if (minX > mesh.Vertices[i].X) {
					minX = mesh.Vertices[i].X;
				}

				if (maxY < mesh.Vertices[i].Y) {
					maxY = mesh.Vertices[i].Y;
				}

				if (minY > mesh.Vertices[i].Y) {
					minY = mesh.Vertices[i].Y;
				}

				if (maxZ < mesh.Vertices[i].Z) {
					maxZ = mesh.Vertices[i].Z;
				}

				if (minZ > mesh.Vertices[i].Z) {
					minZ = mesh.Vertices[i].Z;
				}
			}

			MiddlePoint.X = (maxX + minX) * 0.5f;
			MiddlePoint.Y = (maxY + minY) * 0.5f;
			MiddlePoint.Z = (maxZ + minZ) * 0.5f;

			return mesh;
		}

		protected override float BaseHeight(Table.Table table)
		{
			return 0f;
		}

	}
}
