// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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
			// i dont understand the calculation of splineaccuracy here /cupiii
			var accuracy = (int)(10.0f * 1.3f);
			if (acc != -1)
			{ // hit shapes and UI display have the same, static, precision
				accuracy = acc;
			}

			var splineAccuracy = acc != -1 ? 4.0f * MathF.Pow(10.0f, (10.0f - PhysicsConstants.HitShapeDetailLevel) * (float)(1.0 / 1.5)) : -1.0f;
			SplineVertex sv = new SplineVertex(_data.DragPoints, (int)(_data.Thickness + 0.5), detailLevel, splineAccuracy, margin: margin, loop: true);

			var height = playfieldHeight + meshHeight;


			// one ring for each Splinevertex

			var numRings = sv.VertexCount;
			var numSegments = accuracy;

			var points = new Vertex3D[numRings]; // middlepoints of rings
			var tangents = new Vertex3D[numRings]; // pointing into the direction of the spline, even first and last
			var right = new Vertex3D[numRings]; // pointing right, looking into tangent direction
			var down = new Vertex3D[numRings]; // pointing down from tangent view
			var accLength = new float[numRings]; // accumulated length of the wire beginning at 0;

			// copy the data from the pline into the middle of the new variables
			for (int i = 0; i < sv.VertexCount; i++)
			{
				points[i] = new Vertex3D(sv.MiddlePoints[i].X, sv.MiddlePoints[i].Y, height);
				right[i] = new Vertex3D(sv.RgvLocal[i].X - sv.MiddlePoints[i].X, sv.RgvLocal[i].Y - sv.MiddlePoints[i].Y, 0f);
				right[i].Normalize();
				tangents[i] = Vertex3D.CrossProduct(right[i], new Vertex3D(0f, 0f, 1f));
				tangents[i].Normalize();
			}

			// calculate downvectors
			for (int i = 0; i < numRings; i++)
			{
				down[i] = Vertex3D.CrossProduct(right[i], tangents[i]);
				down[i].Normalize();
			}

			// For UV calculation we need the whole length of the rubber
			accLength[0] = 0.0f;
			for (int i = 1; i < numRings; i++)
				accLength[i] = accLength[i - 1] + (points[i] - points[i - 1]).Length();
			// add the length from the last ring to the first ring
			var totalLength = accLength[numRings - 1] + (points[0] - points[numRings - 1]).Length();  ;

			var numVertices = (numRings + 1) * numSegments;
			var numIndices = numRings * numSegments * 6;
			mesh.Vertices = new Vertex3DNoTex2[numVertices];
			mesh.Indices = new int[numIndices];

			// precalculate the rings (positive X is left, positive Y is up) Starting at the bottom clockwise (X=0, Y=1)
			var ringsX = new float[numSegments];
			var ringsY = new float[numSegments];
			for (int i = 0; i < numSegments; i++)
			{
				ringsX[i] = -1.0f * (float)System.Math.Sin(System.Math.PI * 2 * i / numSegments) * _data.Thickness * 0.5f;
				ringsY[i] = -1.0f * (float)System.Math.Cos(System.Math.PI + System.Math.PI * 2 * i / numSegments) * _data.Thickness * 0.5f;
			}

			var verticesIndex = 0;
			var indicesIndex = 0;

			// calculate Vertices first
			for (int currentRing = 0; currentRing < numRings; currentRing++)
			{
				// calculate one ring
				for (int currentSegment = 0; currentSegment < numSegments; currentSegment++)
				{
					mesh.Vertices[verticesIndex++] = new Vertex3DNoTex2
					{
						X = points[currentRing].X + right[currentRing].X * ringsX[currentSegment] + down[currentRing].X * ringsY[currentSegment],
						Y = points[currentRing].Y + right[currentRing].Y * ringsX[currentSegment] + down[currentRing].Y * ringsY[currentSegment],
						Z = points[currentRing].Z + right[currentRing].Z * ringsX[currentSegment] + down[currentRing].Z * ringsY[currentSegment],
						//normals seem to be somehow off, but are calculated again at the end of mesh creation.
						Nx = right[currentRing].X * ringsX[currentSegment] + down[currentRing].X * ringsY[currentSegment],
						Ny = right[currentRing].Y * ringsX[currentSegment] + down[currentRing].Y * ringsY[currentSegment],
						Nz = right[currentRing].Z * ringsX[currentSegment] + down[currentRing].Z * ringsY[currentSegment],
						Tu = accLength[currentRing] / totalLength,
						Tv = (float)currentSegment / ((float)numSegments - 1)

					};
				}


				// could be integrated in above for loop, but better to read and will be optimized anyway by compiler
				if (currentRing > 0)
				{
					for (int currentSegment = 0; currentSegment < numSegments; currentSegment++)
					{
						var csp1 = currentSegment + 1;
						if (csp1 >= numSegments)
							csp1 = 0;
						mesh.Indices[indicesIndex++] = (currentRing - 1) * numSegments + currentSegment;
						mesh.Indices[indicesIndex++] = currentRing * numSegments + currentSegment;
						mesh.Indices[indicesIndex++] = currentRing * numSegments + csp1;
						mesh.Indices[indicesIndex++] = (currentRing - 1) * numSegments + currentSegment;
						mesh.Indices[indicesIndex++] = currentRing * numSegments + csp1;
						mesh.Indices[indicesIndex++] = (currentRing - 1) * numSegments + csp1;
					}
				}
			}

			// copy first ring into last ring
			for (int currentSegment = 0; currentSegment < numSegments; currentSegment++)
			{
				mesh.Vertices[verticesIndex++] = new Vertex3DNoTex2
				{
					X = points[0].X + right[0].X * ringsX[currentSegment] + down[0].X * ringsY[currentSegment],
					Y = points[0].Y + right[0].Y * ringsX[currentSegment] + down[0].Y * ringsY[currentSegment],
					Z = points[0].Z + right[0].Z * ringsX[currentSegment] + down[0].Z * ringsY[currentSegment],
					//normals seem to be somehow off, but are caculated again at the end of mesh creation.
					Nx = right[0].X * ringsX[currentSegment] + down[0].X * ringsY[currentSegment],
					Ny = right[0].Y * ringsX[currentSegment] + down[0].Y * ringsY[currentSegment],
					Nz = right[0].Z * ringsX[currentSegment] + down[0].Z * ringsY[currentSegment],
					Tu = 1f,
					Tv = (float)currentSegment / ((float)numSegments - 1)

				};
			}

			for (int currentSegment = 0; currentSegment < numSegments; currentSegment++)
			{
				var csp1 = currentSegment + 1;
				if (csp1 >= numSegments)
					csp1 = 0;
				mesh.Indices[indicesIndex++] = (numRings - 1) * numSegments + currentSegment;
				mesh.Indices[indicesIndex++] = (numRings) * numSegments + currentSegment;
				mesh.Indices[indicesIndex++] = (numRings) * numSegments + csp1;
				mesh.Indices[indicesIndex++] = (numRings - 1) * numSegments + currentSegment;
				mesh.Indices[indicesIndex++] = (numRings) * numSegments + csp1;
				mesh.Indices[indicesIndex++] = (numRings - 1) * numSegments + csp1;
			}

			Mesh.ComputeNormals(mesh.Vertices, numVertices, mesh.Indices, numIndices);

			var maxX = Constants.FloatMin;
			var minX = Constants.FloatMax;
			var maxY = Constants.FloatMin;
			var minY = Constants.FloatMax;
			var maxZ = Constants.FloatMin;
			var minZ = Constants.FloatMax;
			for (var i = 0; i < numVertices; i++)
			{
				maxX = MathF.Max(maxX, mesh.Vertices[i].X);
				maxY = MathF.Max(maxY, mesh.Vertices[i].Y);
				maxZ = MathF.Max(maxZ, mesh.Vertices[i].Z);
				minX = MathF.Min(minX, mesh.Vertices[i].X);
				minY = MathF.Min(minY, mesh.Vertices[i].X);
				minZ = MathF.Min(minZ, mesh.Vertices[i].X);
			}

			_middlePoint.X = (maxX + minX) * 0.5f;
			_middlePoint.Y = (maxY + minY) * 0.5f;
			_middlePoint.Z = (maxZ + minZ) * 0.5f;

			return mesh;

		}

	}
}
