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
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.MetalWireGuide
{
	public class MetalWireGuideMeshGenerator
	{
		private readonly IMetalWireGuideData _data;

		private Vertex3D _middlePoint;

		public MetalWireGuideMeshGenerator(IMetalWireGuideData data)
		{
			_data = data;
		}

		public Mesh GetTransformedMesh(float playfieldHeight, float meshHeight, int detailLevel, float bendradius, int acc = -1, bool createHitShape = false, float margin = 0f)
		{
			var mesh = GetMesh(playfieldHeight, meshHeight, detailLevel, bendradius, acc, createHitShape, margin);
			return mesh.Transform(GetRotationMatrix());
		}

		public Mesh GetMesh(Table.Table table, MetalWireGuideData metalWireGuideData)
		{
			var mesh = GetTransformedMesh(table.TableHeight, _data.Height, table.GetDetailLevel(), _data.Bendradius);
			mesh.Name = metalWireGuideData.Name;
			var preMatrix = new Matrix3D();
			preMatrix.SetTranslation(0, 0, -_data.Height);
			return mesh.Transform(preMatrix);
		}

		public PbrMaterial GetMaterial(Table.Table table, MetalWireGuideData metalWireGuideData)
		{
			return new PbrMaterial(table.GetMaterial(metalWireGuideData.Material), table.GetTexture(metalWireGuideData.Image));
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

		private Mesh GetMesh(float playfieldHeight, float meshHeight, int detailLevel, float bendradius, int acc = -1, bool createHitShape = false, float margin = 0f)
		{
			var mesh = new Mesh();
			// i dont understand the calculation of splineaccuracy here /cupiii
			var accuracy = (int)(10.0f * 1.2f);
			if (acc != -1) { // hit shapes and UI display have the same, static, precision
				accuracy = acc;
			}

			var splineAccuracy = acc != -1 ? 4.0f * MathF.Pow(10.0f, (10.0f - PhysicsConstants.HitShapeDetailLevel) * (float)(1.0 / 1.5)) : -1.0f;
			SplineVertex sv = new SplineVertex(_data.DragPoints, (int)(_data.Thickness+0.5), detailLevel, splineAccuracy, margin: margin, loop: false);

			var height = playfieldHeight + meshHeight;

			var standheight = _data.Standheight;
			// dont lat the Collider be higher than the visible mesh, just shift the top of the MWG.
			if (createHitShape)
				standheight = standheight - _data.Height + height;


			// one ring for each Splinevertex, two for the stands, and "bendradius" tomes two for the bend (should be enough)
			// todo: could be better, if accuracy was taken into account
			var numRingsInBend = (int)(bendradius + 1);
			var numRings = sv.VertexCount-1 + numRingsInBend * 2 + 2;
			var numSegments = accuracy;

			var up = new Vertex3D(0f, 0f, 1f);
			var points = new Vertex3D[numRings]; // middlepoints of rings
			var tangents = new Vertex3D[numRings]; // pointing into the direction of the spline, even first and last
			var right = new Vertex3D[numRings]; // pointing right, looking into tangent direction
			var down = new Vertex3D[numRings]; // pointing down from tangent view
			var accLength = new float[numRings]; // accumulated length of the wire beginning at 0;

			// copy the data from the pline into the middle of the new variables
			for (int i = 0; i < sv.VertexCount-1; i++)
			{
				points[i + numRingsInBend + 1] = new Vertex3D(sv.MiddlePoints[i].X, sv.MiddlePoints[i].Y, height);
				right[i + numRingsInBend + 1] = new Vertex3D(sv.RgvLocal[i].X - sv.MiddlePoints[i].X, sv.RgvLocal[i].Y - sv.MiddlePoints[i].Y, 0f);
				right[i + numRingsInBend + 1].Normalize();
				tangents[i + numRingsInBend + 1] = Vertex3D.CrossProduct(right[i + numRingsInBend + 1], new Vertex3D(0f, 0f, 1f));
				tangents[i + numRingsInBend + 1].Normalize();
			}

			// first set up beginning of the stand
			points[0] = points[numRingsInBend + 1] + tangents[numRingsInBend + 1] * bendradius * -1 + up * standheight * -1f;
			tangents[0] = new Vertex3D(0f, 0f, 1f);
			right[0] = right[numRingsInBend+1];
			// set up the first point of the bend
			points[1] = points[numRingsInBend + 1] + tangents[numRingsInBend + 1] * bendradius * -1 + up * bendradius * -1f;
			tangents[1] = tangents[0];
			right[1] = right[0];
			// now bend from point 1 to numRingsInBend+1(-1)
			var diffXY = points[numRingsInBend + 1] - points[1];
			diffXY.Z = 0;
			var diffZ = points[numRingsInBend + 1] - points[1];
			diffZ.X = 0;
			diffZ.Y = 0;
			for (int i = 1; i < (numRingsInBend+1); i++)
			{

				points[numRingsInBend - i + 1] = points[1] + diffXY - (float)System.Math.Sin(System.Math.PI / 2 / numRingsInBend * i) * diffXY + (float)System.Math.Cos(System.Math.PI / 2 / numRingsInBend * i) * diffZ;
				var tmp = tangents[numRingsInBend + 1];
				tmp.Normalize();
				tangents[numRingsInBend - i + 1] = tmp * (float)System.Math.Cos(System.Math.PI / 2 / numRingsInBend * i)  + (float)System.Math.Sin(System.Math.PI / 2 / numRingsInBend * i) * up;
				right[numRingsInBend - i + 1] = right[0];
			}
			// set up last point
			points[numRings-1] = points[(numRings - 1) - numRingsInBend - 1] + tangents[numRings - 1 - numRingsInBend - 1] * bendradius + up * standheight * -1f;
			tangents[numRings-1] = new Vertex3D(0f, 0f, -1f);
			right[numRings - 1] = right[(numRings - 1) - numRingsInBend - 1];
			// and the point before
			points[numRings-2] = points[(numRings - 1) - numRingsInBend - 1] + tangents[numRings - 1 - numRingsInBend - 1] * bendradius + up * bendradius * -1f;
			tangents[numRings - 2] = tangents[numRings - 1];
			right[numRings - 2] = right[numRings - 1];
			// now bend again
			diffXY = points[numRings - 1 - numRingsInBend - 1] - points[numRings-2];
			diffXY.Z = 0;
			diffZ = points[numRings - 1 - numRingsInBend - 1] - points[numRings - 2];
			diffZ.X = 0;
			diffZ.Y = 0;
			for (int i = 1; i < (numRingsInBend + 1); i++)
			{

				points[numRings - 2 - numRingsInBend + i] = points[numRings - 2] + diffXY - (float)System.Math.Sin(System.Math.PI / 2 / numRingsInBend * i) * diffXY + (float)System.Math.Cos(System.Math.PI / 2 / numRingsInBend * i) * diffZ;
				var tmp = tangents[numRings - 1 - numRingsInBend - 1];
				tmp.Normalize();
				tangents[numRings - 2 - numRingsInBend + i] = tmp * (float)System.Math.Cos(System.Math.PI / 2 / numRingsInBend * i) + (float)System.Math.Sin(System.Math.PI / 2 / numRingsInBend * i) * up*-1;
				right[numRings - 2 - numRingsInBend + i] = right[numRings-1];
			}

			// calculate downvectors
			for (int i = 0; i < numRings; i++)
			{
				down[i] = Vertex3D.CrossProduct(right[i], tangents[i]);
				down[i].Normalize();
			}

			// For UV calculation we need the whole length of the wire
			accLength[0] = 0.0f;
			for (int i = 1; i < numRings; i++)
				accLength[i] = accLength[i - 1] + (points[i]-points[i-1]).Length();
			var totalLength = accLength[numRings-1];

			var numVertices = numRings * numSegments;
			var numIndices = (numRings-1) * numSegments * 6;
			mesh.Vertices = new Vertex3DNoTex2[numVertices];
			mesh.Indices = new int[numIndices];

			// precalculate the rings (positive X is left, positive Y is up) Starting at the bottom clockwise (X=0, Y=1)
			var ringsX = new float[numSegments];
			var ringsY = new float[numSegments];
			for (int i = 0; i < numSegments;i++)
			{
				ringsX[i] = -1.0f * (float)System.Math.Sin(System.Math.PI*2 * i / numSegments) * _data.Thickness;
				ringsY[i] = -1.0f * (float)System.Math.Cos(System.Math.PI + System.Math.PI*2 * i / numSegments) * _data.Thickness;
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
						//normals seem to be somehow off, but are caculated again at the end of mesh creation.
						Nx = right[currentRing].X * ringsX[currentSegment] + down[currentRing].X * ringsY[currentSegment],
						Ny = right[currentRing].Y * ringsX[currentSegment] + down[currentRing].Y * ringsY[currentSegment],
						Nz = right[currentRing].Z * ringsX[currentSegment] + down[currentRing].Z * ringsY[currentSegment],
						Tu = accLength[currentRing] / totalLength,
						Tv = (float)currentSegment/((float)numSegments-1)

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

			Mesh.ComputeNormals(mesh.Vertices, numVertices, mesh.Indices, numIndices);

			var maxX = Constants.FloatMin;
			var minX = Constants.FloatMax;
			var maxY = Constants.FloatMin;
			var minY = Constants.FloatMax;
			var maxZ = Constants.FloatMin;
			var minZ = Constants.FloatMax;
			for (var i = 0; i < numVertices; i++)
			{
				MathF.Max(maxX, mesh.Vertices[i].X);
				MathF.Max(maxY, mesh.Vertices[i].Y);
				MathF.Max(maxZ, mesh.Vertices[i].Z);
				MathF.Min(minX, mesh.Vertices[i].X);
				MathF.Min(minY, mesh.Vertices[i].X);
				MathF.Min(minZ, mesh.Vertices[i].X);
			}

			_middlePoint.X = (maxX + minX) * 0.5f;
			_middlePoint.Y = (maxY + minY) * 0.5f;
			_middlePoint.Z = (maxZ + minZ) * 0.5f;

			return mesh;
		}
	}
}
