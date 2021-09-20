// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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

// ReSharper disable CompareOfFloatsByEqualityOperator

#nullable enable

using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Rubber
{
	public class RubberMeshGenerator
	{
		private readonly IRubberData _data;

		private Vertex3D _middlePoint;

		public RubberMeshGenerator(IRubberData data)
		{
			_data = data;
		}

		public Mesh GetTransformedMesh(float playfieldHeight, float meshHeight, int detailLevel, int acc = -1, bool createHitShape = false, float margin = 0f)
		{
			var mesh = GetMesh(playfieldHeight, meshHeight, detailLevel, acc, createHitShape, margin);
			return mesh.Transform(GetRotationMatrix());
		}

		public Mesh GetMesh(Table.Table table, RubberData rubberData)
		{
			var mesh = GetTransformedMesh(table.TableHeight, _data.Height, table.GetDetailLevel());
			mesh.Name = rubberData.Name;
			var preMatrix = new Matrix3D();
			preMatrix.SetTranslation(0, 0, -_data.Height);
			return mesh.Transform(preMatrix);
		}

		public PbrMaterial GetMaterial(Table.Table table, RubberData rubberData)
		{
			return new PbrMaterial(table.GetMaterial(rubberData.Material), table.GetTexture(rubberData.Image));
		}

		private Matrix3D GetRotationMatrix()
		{
			var fullMatrix = new Matrix3D();
			var tempMat = new Matrix3D();

			tempMat.SetTranslation(-_middlePoint.X, -_middlePoint.Y, -_middlePoint.Z);
			fullMatrix.Multiply(tempMat, fullMatrix);

			tempMat.RotateZMatrix(MathF.DegToRad(_data.RotZ));
			fullMatrix.Multiply(tempMat);
			tempMat.RotateYMatrix(MathF.DegToRad(_data.RotY));
			fullMatrix.Multiply(tempMat);
			tempMat.RotateXMatrix(MathF.DegToRad(_data.RotX));
			fullMatrix.Multiply(tempMat);

			tempMat.SetTranslation(_middlePoint.X, _middlePoint.Y, _middlePoint.Z);
			fullMatrix.Multiply(tempMat);

			return fullMatrix;
		}

		private Mesh GetMesh(float playfieldHeight, float meshHeight, int detailLevel, int acc = -1, bool createHitShape = false, float margin = 0f)
		{
			var mesh = new Mesh();
			var accuracy = (int)(10.0f * 1.2f);
			if (acc != -1) { // hit shapes and UI display have the same, static, precision
				accuracy = acc;
			}

			var splineAccuracy = acc != -1 ? 4.0f * MathF.Pow(10.0f, (10.0f - PhysicsConstants.HitShapeDetailLevel) * (float) (1.0 / 1.5)) : -1.0f;
			var sv = new SplineVertex(_data.DragPoints, _data.Thickness, detailLevel, splineAccuracy, margin: margin);

			var numRings = sv.VertexCount - 1;
			var numSegments = accuracy;

			var numVertices = numRings * numSegments;
			var numIndices = 6 * numVertices; //m_numVertices*2+2;
			var height = playfieldHeight + meshHeight;

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
					normal = Vertex3D.CrossProduct(prevB, tangent);
					biNormal = Vertex3D.CrossProduct(tangent, normal);
				}

				biNormal.Normalize();
				normal.Normalize();
				prevB = biNormal;
				var u = i * invNr;
				for (var j = 0; j < numSegments; j++) {
					var v = ((float)j + u) * invNs;
					var tmp = Vertex3D.GetRotatedAxis(j * (360.0f * invNs), tangent, normal) * ((_data.Thickness + margin) * 0.5f);

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

			_middlePoint.X = (maxX + minX) * 0.5f;
			_middlePoint.Y = (maxY + minY) * 0.5f;
			_middlePoint.Z = (maxZ + minZ) * 0.5f;

			return mesh;
		}
	}
}
