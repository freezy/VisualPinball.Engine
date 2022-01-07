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
			SplineVertex sv = new SplineVertex(_data.DragPoints, _data.Thickness, detailLevel, splineAccuracy, margin: margin, loop: false);

			var height = playfieldHeight + meshHeight;
			// hack - Component has to get edited.
			var standheight = 50;

			// one ring for each Splinevertex, two for the stands, and "bendradius" tomes two for the bend (should be enough) 
			// todo: could be better, if accuracy was taken into account
			var numRingsInBend = (int)(bendradius + 1);
			var numRings = sv.VertexCount-1 + numRingsInBend * 2 + 2;

			var up = new Vertex3D(0f, 0f, 1f);
			var points = new Vertex3D[numRings]; // middlepoints of rings
			var tangents = new Vertex3D[numRings]; // pointing into the direction of the spline, even first and last
			var right = new Vertex3D[numRings]; // pointing right, looking into tangent direction with up=up
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

				points[i + 1] = points[1] +diffXY - (float)System.Math.Sin(System.Math.PI/2 / numRingsInBend * i) * diffXY + (float)System.Math.Cos(System.Math.PI/2 / numRingsInBend* i) * diffZ;
				var tmp = tangents[numRingsInBend + 1];
				tmp.Normalize();
				tangents[i + 1] = tmp * (float)System.Math.Cos(System.Math.PI / 2 / numRingsInBend * i)  + (float)System.Math.Sin(System.Math.PI / 2 / numRingsInBend * i) * up;
				right[i + 1] = right[0];
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

				points[numRings-2-i] = points[numRings-2] + diffXY - (float)System.Math.Sin(System.Math.PI / 2 / numRingsInBend * i) * diffXY + (float)System.Math.Cos(System.Math.PI / 2 / numRingsInBend * i) * diffZ;
				var tmp = tangents[numRings - 1 - numRingsInBend - 1];
				tmp.Normalize();
				tangents[numRings - 2 - i] = tmp * (float)System.Math.Cos(System.Math.PI / 2 / numRingsInBend * i) + (float)System.Math.Sin(System.Math.PI / 2 / numRingsInBend * i) * up*-1;
				right[numRings - 2 - i] = right[numRings-1];
			}

			// For UV calculation we need the whole length of the wire
			accLength[0] = 0.0f;
			for (int i = 1; i < numRings; i++)
				accLength[i] = accLength[i - 1] + (points[i]-points[i-1]).Length();
			var totalLength = accLength[numRings];

			var numVertices = numRings*3;
			var numIndices = numRings*3;
			mesh.Vertices = new Vertex3DNoTex2[numVertices];
			mesh.Indices = new int[numIndices];


			

			var verticesIndex = 0;


			for (int i = 0; i < numRings; i++)
			{
				/*
				var tmp1 = new Vertex3D
				{
					X = sv.MiddlePoints[i].X-sv.RgvLocal[numRings*2-i-1].X,
					Y = sv.MiddlePoints[i].Y-sv.RgvLocal[numRings * 2 - i - 1].Y,
					Z = 0
				};
				*/
				var tmp1 = right[i];
				tmp1.Normalize();
				tmp1 *= 10;

//				tmp1.X = 5;
//				tmp1.Y = 5;

				var tmp2 = tangents[i];
				tmp2.Normalize();
				tmp2 *= 5;

				// tmp2 = tangent!
				mesh.Vertices[verticesIndex] = new Vertex3DNoTex2
				{
					X = points[i].X,
					Y = points[i].Y,
					Z = points[i].Z
				};

				mesh.Indices[verticesIndex] = verticesIndex;
				mesh.Vertices[verticesIndex+1] = new Vertex3DNoTex2
				{
					X = points[i].X + tmp1.X + tmp2.X,
					Y = points[i].Y + tmp1.Y + tmp2.Y,
					Z = points[i].Z + tmp1.Z + tmp2.Z
				};
				mesh.Indices[verticesIndex+1] = verticesIndex+1;
				mesh.Vertices[verticesIndex + 2] = new Vertex3DNoTex2
				{
					X = points[i].X + tmp1.X - tmp2.X * 0,
					Y = points[i].Y + tmp1.Y - tmp2.Y * 0,
					Z = points[i].Z + tmp1.Z - tmp2.Z * 0
				};
				mesh.Indices[verticesIndex+2] = verticesIndex+2;
				verticesIndex += 3;
			}
			Mesh.ComputeNormals(mesh.Vertices, numVertices, mesh.Indices, numIndices);

			// so now we have all centers of the ring points in sv.
			// normals are sv.RgvLocal - sv.Middlepoints


			/*

			// these rings should all point up.
			// i should calculate all vertices in 3d, all tangets. 
			// and after that i should add the beginning and End Vertices (bent wires).


			// these are the vertices of the spline.
			var numRings =  sv.VertexCount;
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
			 
			for (var i = 0; i < numRings-1; i++) {
				// Straigten up one end
				var i2 = i == numRings - 1 ? 0 : i + 1;
				// Straigten up the other end
				if (i == numRings - 2)
					i2 -= 2;
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

			*/
			/* garbage
			//add some more rings here for the wirebend
			// add some vertices for the bend (half of bendradius rounded to ceiling (+1)) and one for the stand times 2
			var ringsToAdd = (int)(bendradius / 2f) + 1 + 1;
			for (var i = 0; i < (int)(bendradius / 2f) + 1; i++)
			{

			}*/
			/*
			// calculate faces
			for (var i = 0; i < numRings-2; i++) {
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
			*/
			return mesh;
		}
	}
}
