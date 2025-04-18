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

#region ReSharper
// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable CommentTypo
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using MathF = VisualPinball.Engine.Math.MathF;

namespace VisualPinball.Engine.VPT.Ramp
{
	public class RampMeshGenerator
	{
		public const string Wall = "Wall";
		public const string Floor = "Floor";
		public const string Wires = "Wires";

		private readonly IRampData _data;
		private readonly Vertex3D _position;

		public RampMeshGenerator(IRampData data, Vertex3D position)
		{
			_data = data;
			_position = position;
		}

		public Mesh GetMesh(float tableWidth, float tableHeight, float heightZ, string id)
		{
			var mesh = new Mesh();
			if (id == Wires) {
				var meshes = GenerateWireMeshes(heightZ);

				for (var i = 1; i <= 4; i++) {
					var name = $"Wire{i}";
					if (meshes.ContainsKey(name)) {
						mesh.Merge(meshes[name]);
					}
				}

			} else {
				var rv = GetRampVertex(heightZ, -1, true);
				switch (id) {
					case Floor:
						mesh = GenerateFlatFloorMesh(tableWidth, tableHeight, rv);
						break;

					case Wall:
						if (_data.RightWallHeightVisible > 0.0) {
							mesh = mesh.Merge(GenerateFlatRightWall(tableWidth, tableHeight, rv));
						}

						if (_data.LeftWallHeightVisible > 0.0) {
							mesh = mesh.Merge(GenerateFlatLeftWall(tableWidth, tableHeight, rv));
						}
						break;
				}

				if (mesh.Vertices == null) {
					mesh.Vertices = Array.Empty<Vertex3DNoTex2>();
					mesh.Indices = Array.Empty<int>();
				}
			}

			return mesh;
		}

		public Mesh GetMesh(string id, Table.Table table, bool asRightHanded)
		{
			var meshes = GenerateMeshes(table.Width, table.Height, 0);
			if (meshes.ContainsKey(id)) {
				return asRightHanded ? meshes[id].Transform(Matrix3D.RightHanded) : meshes[id];
			}

			throw new ArgumentException($"No mesh with ID \"{id}\" found. Valid IDs: {string.Join(", ", meshes.Keys)}.");
		}

		public PbrMaterial GetMaterial(Table.Table table, RampData rampData)
		{
			return new PbrMaterial(table.GetMaterial(rampData.Material), table.GetTexture(rampData.Image));
		}

		private Dictionary<string, Mesh> GenerateMeshes(float tableWidth, float tableHeight, float heightZ)
		{
			return !IsHabitrail()
				? GenerateFlatMesh(tableWidth, tableHeight, heightZ)
				: GenerateWireMeshes(heightZ);
		}

		private Dictionary<string, Mesh> GenerateWireMeshes(float tableHeight)
		{
			var meshes = new Dictionary<string, Mesh>();
			var (wireMeshA, wireMeshB) = GenerateBaseWires(tableHeight);
			switch (_data.Type) {
				case RampType.RampType1Wire: {
					wireMeshA.Name = "Wire1";
					meshes["Wire1"] = wireMeshA;
					break;
				}
				case RampType.RampType2Wire: {
					var wire1Mesh = wireMeshA.MakeTranslation(0f, 0f, 3.0f);
					var wire2Mesh = wireMeshB.MakeTranslation(0f, 0f, 3.0f);
					wire1Mesh.Name = "Wire1";
					wire2Mesh.Name = "Wire2";
					meshes["Wire1"] = wire1Mesh;
					meshes["Wire2"] = wire2Mesh;
					break;
				}
				case RampType.RampType4Wire: {
					meshes["Wire1"] = wireMeshA.Clone("Wire1").MakeTranslation(0f, 0f, _data.WireDistanceY * 0.5f);
					meshes["Wire2"] = wireMeshB.Clone("Wire2").MakeTranslation(0f, 0f, _data.WireDistanceY * 0.5f);
					meshes["Wire3"] = wireMeshA.MakeTranslation(0f, 0f, 3.0f);
					meshes["Wire3"].Name = "Wire3";
					meshes["Wire4"] = wireMeshB.MakeTranslation(0f, 0f, 3.0f);
					meshes["Wire4"].Name = "Wire4";
					break;
				}
				case RampType.RampType3WireLeft: {
					meshes["Wire2"] = wireMeshB.Clone("Wire2").MakeTranslation(0f, 0f, _data.WireDistanceY * 0.5f);
					meshes["Wire3"] = wireMeshA.MakeTranslation(0f, 0f, 3.0f);
					meshes["Wire3"].Name = "Wire3";
					meshes["Wire4"] = wireMeshB.MakeTranslation(0f, 0f, 3.0f);
					meshes["Wire4"].Name = "Wire4";
					break;
				}
				case RampType.RampType3WireRight: {
					meshes["Wire1"] = wireMeshA.Clone("Wire1").MakeTranslation(0f, 0f, _data.WireDistanceY * 0.5f);
					meshes["Wire3"] = wireMeshA.MakeTranslation(0f, 0f, 3.0f);
					meshes["Wire3"].Name = "Wire3";
					meshes["Wire4"] = wireMeshB.MakeTranslation(0f, 0f, 3.0f);
					meshes["Wire4"].Name = "Wire4";
					break;
				}
			}
			return meshes;
		}

		private Dictionary<string, Mesh> GenerateFlatMesh(float tableWidth, float tableHeight, float heightZ) {
			var meshes = new Dictionary<string, Mesh>();
			var rv = GetRampVertex(heightZ, -1, true);

			meshes["Floor"] = GenerateFlatFloorMesh(tableWidth, tableHeight, rv);

			if (_data.RightWallHeightVisible > 0.0) {
				meshes["RightWall"] = GenerateFlatRightWall(tableWidth, tableHeight, rv);
			}

			if (_data.LeftWallHeightVisible > 0.0) {
				meshes["LeftWall"] = GenerateFlatLeftWall(tableWidth, tableHeight, rv);
			}

			return meshes;
		}

		private Mesh GenerateFlatFloorMesh(float tableWidth, float tableHeight, RampVertex rv)
		{
			var invTableWidth = 1.0f / tableWidth;
			var invTableHeight = 1.0f / tableHeight;
			var numVertices = rv.VertexCount * 2;
			var numIndices = (rv.VertexCount - 1) * 6;

			var mesh = new Mesh("Floor") {
				Vertices = new Vertex3DNoTex2[numVertices],
				Indices = new int[numIndices]
			};
			for (var i = 0; i < rv.VertexCount; i++) {
				var rgv3d1 = new Vertex3DNoTex2();
				var rgv3d2 = new Vertex3DNoTex2();

				rgv3d1.X = rv.RgvLocal[i].X;
				rgv3d1.Y = rv.RgvLocal[i].Y;
				rgv3d1.Z = rv.PointHeights[i];

				rgv3d2.X = rv.RgvLocal[rv.VertexCount * 2 - i - 1].X;
				rgv3d2.Y = rv.RgvLocal[rv.VertexCount * 2 - i - 1].Y;
				rgv3d2.Z = rgv3d1.Z;

				//if (_data.Image != null) {
					if (_data.ImageAlignment == RampImageAlignment.ImageModeWorld) {
						rgv3d1.Tu = (_position.X + rgv3d1.X) * invTableWidth;
						rgv3d1.Tv = (_position.Y + rgv3d1.Y) * invTableHeight;
						rgv3d2.Tu = (_position.X + rgv3d2.X) * invTableWidth;
						rgv3d2.Tv = (_position.Y + rgv3d2.Y) * invTableHeight;

					} else {
						rgv3d1.Tu = 1.0f;
						rgv3d1.Tv = rv.PointRatios[i];
						rgv3d2.Tu = 0.0f;
						rgv3d2.Tv = rv.PointRatios[i];
					}

				// } else {
				// 	rgv3d1.Tu = 0.0f;
				// 	rgv3d1.Tv = 0.0f;
				// 	rgv3d2.Tu = 0.0f;
				// 	rgv3d2.Tv = 0.0f;
				// }

				mesh.Vertices[i * 2] = rgv3d1;
				mesh.Vertices[i * 2 + 1] = rgv3d2;

				if (i == rv.VertexCount - 1) {
					break;
				}

				mesh.Indices[i * 6] = i * 2;
				mesh.Indices[i * 6 + 1] = i * 2 + 1;
				mesh.Indices[i * 6 + 2] = i * 2 + 3;
				mesh.Indices[i * 6 + 3] = i * 2;
				mesh.Indices[i * 6 + 4] = i * 2 + 3;
				mesh.Indices[i * 6 + 5] = i * 2 + 2;
			}

			Mesh.ComputeNormals(mesh.Vertices, numVertices, mesh.Indices, (rv.VertexCount - 1) * 6);
			return mesh;
		}

		private Mesh GenerateFlatLeftWall(float tableWidth, float tableHeight, RampVertex rv)
		{
			var invTableWidth = 1.0f / tableWidth;
			var invTableHeight = 1.0f / tableHeight;
			var numVertices = rv.VertexCount * 2;
			var numIndices = (rv.VertexCount - 1) * 6;

			var mesh = new Mesh("LeftWall") {
				Vertices = new Vertex3DNoTex2[numVertices],
				Indices = new int[numIndices]
			};
			for (var i = 0; i < rv.VertexCount; i++) {
				var rgv3d1 = new Vertex3DNoTex2();
				var rgv3d2 = new Vertex3DNoTex2();

				rgv3d1.X = rv.RgvLocal[rv.VertexCount * 2 - i - 1].X;
				rgv3d1.Y = rv.RgvLocal[rv.VertexCount * 2 - i - 1].Y;
				rgv3d1.Z = rv.PointHeights[i];

				rgv3d2.X = rgv3d1.X;
				rgv3d2.Y = rgv3d1.Y;
				rgv3d2.Z = (rv.PointHeights[i] + _data.LeftWallHeightVisible);

				if (_data.ImageAlignment == RampImageAlignment.ImageModeWorld) {
					rgv3d1.Tu = (_position.X + rgv3d1.X) * invTableWidth;
					rgv3d1.Tv = (_position.Y + rgv3d1.Y) * invTableHeight;

				} else {
					rgv3d1.Tu = 0;
					rgv3d1.Tv = rv.PointRatios[i];
				}

				rgv3d2.Tu = rgv3d1.Tu;
				rgv3d2.Tv = rgv3d1.Tv;

				mesh.Vertices[i * 2] = rgv3d1;
				mesh.Vertices[i * 2 + 1] = rgv3d2;

				if (i == rv.VertexCount - 1) {
					break;
				}

				mesh.Indices[i * 6] = i * 2;
				mesh.Indices[i * 6 + 1] = i * 2 + 1;
				mesh.Indices[i * 6 + 2] = i * 2 + 3;
				mesh.Indices[i * 6 + 3] = i * 2;
				mesh.Indices[i * 6 + 4] = i * 2 + 3;
				mesh.Indices[i * 6 + 5] = i * 2 + 2;
			}

			Mesh.ComputeNormals(mesh.Vertices, numVertices, mesh.Indices, (rv.VertexCount - 1) * 6);
			return mesh;
		}

		private Mesh GenerateFlatRightWall(float tableWidth, float tableHeight, RampVertex rv)
		{
			var invTableWidth = 1.0f / tableWidth;
			var invTableHeight = 1.0f / tableHeight;
			var numVertices = rv.VertexCount * 2;
			var numIndices = (rv.VertexCount - 1) * 6;

			var mesh = new Mesh("RightWall") {
				Vertices = new Vertex3DNoTex2[numVertices],
				Indices = new int[numIndices]
			};
			for (var i = 0; i < rv.VertexCount; i++) {
				var rgv3d1 = new Vertex3DNoTex2();
				var rgv3d2 = new Vertex3DNoTex2();

				rgv3d1.X = rv.RgvLocal[i].X;
				rgv3d1.Y = rv.RgvLocal[i].Y;
				rgv3d1.Z = rv.PointHeights[i];

				rgv3d2.X = rv.RgvLocal[i].X;
				rgv3d2.Y = rv.RgvLocal[i].Y;
				rgv3d2.Z = (rv.PointHeights[i] + _data.RightWallHeightVisible);

				if (_data.ImageAlignment == RampImageAlignment.ImageModeWorld) {
					rgv3d1.Tu = (_position.X + rgv3d1.X) * invTableWidth;
					rgv3d1.Tv = (_position.Y + rgv3d1.Y) * invTableHeight;

				} else {
					rgv3d1.Tu = 0;
					rgv3d1.Tv = rv.PointRatios[i];
				}
				rgv3d2.Tu = rgv3d1.Tu;
				rgv3d2.Tv = rgv3d1.Tv;


				mesh.Vertices[i * 2] = rgv3d1;
				mesh.Vertices[i * 2 + 1] = rgv3d2;

				if (i == rv.VertexCount - 1) {
					break;
				}

				mesh.Indices[i * 6] = i * 2;
				mesh.Indices[i * 6 + 1] = i * 2 + 1;
				mesh.Indices[i * 6 + 2] = i * 2 + 3;
				mesh.Indices[i * 6 + 3] = i * 2;
				mesh.Indices[i * 6 + 4] = i * 2 + 3;
				mesh.Indices[i * 6 + 5] = i * 2 + 2;
			}

			Mesh.ComputeNormals(mesh.Vertices, numVertices, mesh.Indices, (rv.VertexCount - 1) * 6);
			return mesh;
		}

		private Tuple<Mesh, Mesh> GenerateBaseWires(float tableHeight)
		{
			int accuracy;
			// if (table.GetDetailLevel() < 5) {
			// 	accuracy = 6;
			//
			// } else if (table.GetDetailLevel() >= 5 && table.GetDetailLevel() < 8) {
			// 	accuracy = 8;
			//
			// } else {
			// 	accuracy = (int) (table.GetDetailLevel() * 1.3f); // see below
			// }

			// as solid ramps are rendered into the static buffer, always use maximum precision
			// var mat = table.GetMaterial(_data.Material);
			// if (mat == null || !mat.IsOpacityActive) {
				accuracy = 12; // see above
			//}

			var rv = GetRampVertex(tableHeight, -1, false);
			var middlePoints = rv.MiddlePoints;

			var numRings = rv.VertexCount;
			var numSegments = accuracy;
			var numVertices = numRings * numSegments;
			var numIndices = 6 * numVertices; //m_numVertices*2+2;

			var tmpPoints = new Vertex2D[rv.VertexCount];

			for (var i = 0; i < rv.VertexCount; i++) {
				tmpPoints[i] = rv.RgvLocal[rv.VertexCount * 2 - i - 1];
			}

			Vertex3DNoTex2[] vertBuffer;
			Vertex3DNoTex2[] vertBuffer2;

			if (_data.Type != RampType.RampType1Wire) {
				vertBuffer = CreateWire(numRings, numSegments, rv.RgvLocal, rv.PointHeights);
				vertBuffer2 = CreateWire(numRings, numSegments, tmpPoints, rv.PointHeights);

			} else {
				vertBuffer = CreateWire(numRings, numSegments, middlePoints, rv.PointHeights);
				vertBuffer2 = null;
			}

			// calculate faces
			var indices = new int[numIndices];
			for (var i = 0; i < numRings - 1; i++) {
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

					var offs = (i * numSegments + j) * 6;
					indices[offs] = quad[0];
					indices[offs + 1] = quad[1];
					indices[offs + 2] = quad[2];
					indices[offs + 3] = quad[3];
					indices[offs + 4] = quad[2];
					indices[offs + 5] = quad[1];
				}
			}

			if (_data.Type != RampType.RampType1Wire) {
				return new Tuple<Mesh, Mesh>(
					new Mesh(vertBuffer, indices),
					new Mesh(vertBuffer2, indices)
				);
			}
			return new Tuple<Mesh, Mesh>(new Mesh(vertBuffer, indices), null);
		}

		private Vertex3DNoTex2[] CreateWire(int numRings, int numSegments, IReadOnlyList<Vertex2D> midPoints, IReadOnlyList<float> initialHeights)
		{
			var vertices = new Vertex3DNoTex2[numRings * numSegments];
			var prev = new Vertex3D();
			var index = 0;
			for (var i = 0; i < numRings; i++) {
				var i2 = i == numRings - 1 ? i : i + 1;
				var height = initialHeights[i];

				var tangent = new Vertex3D(
					midPoints[i2].X - midPoints[i].X,
					midPoints[i2].Y - midPoints[i].Y,
					initialHeights[i2] - initialHeights[i]
				);
				if (i == numRings - 1) {
					// for the last spline point use the previous tangent again, otherwise we won't see the complete wire (it stops one control point too early)
					tangent.X = midPoints[i].X - midPoints[i - 1].X;
					tangent.Y = midPoints[i].Y - midPoints[i - 1].Y;
				}

				Vertex3D biNormal;
				Vertex3D normal;
				if (i == 0) {
					var up = new Vertex3D(
						midPoints[i2].X + midPoints[i].X,
						midPoints[i2].Y + midPoints[i].Y,
						initialHeights[i2] - height
					);
					normal = Vertex3D.CrossProduct(tangent, up);     //normal
					biNormal = Vertex3D.CrossProduct(tangent, normal);

				} else {
					normal = Vertex3D.CrossProduct(prev, tangent);
					biNormal = Vertex3D.CrossProduct(tangent, normal);
				}

				biNormal.Normalize();
				normal.Normalize();
				prev = biNormal;

				var invNumRings = 1.0f / numRings;
				var invNumSegments = 1.0f / numSegments;
				var u = i * invNumRings;
				for (var j = 0; j < numSegments; j++, index++) {
					var v = (j + u) * invNumSegments;
					var tmp = Vertex3D.GetRotatedAxis(j * (360.0f * invNumSegments), tangent, normal) * (_data.WireDiameter * 0.5f);

					vertices[index] = new Vertex3DNoTex2 {
						X = midPoints[i].X + tmp.X,
						Y = midPoints[i].Y + tmp.Y,
						Z = height + tmp.Z,
						Tu = u,
						Tv = v
					};

					// normals
					var n = new Vertex3D(
						vertices[index].X - midPoints[i].X,
						vertices[index].Y - midPoints[i].Y,
						vertices[index].Z - height
					);
					var len = 1.0f / MathF.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
					vertices[index].Nx = n.X * len;
					vertices[index].Ny = n.Y * len;
					vertices[index].Nz = n.Z * len;
				}
			}

			return vertices;
		}

		public RampVertex GetRampVertex(float tableHeight, float accuracy, bool incWidth)
		{
			var result = new RampVertex();

			// vvertex are the 2D vertices forming the central curve of the ramp as seen from above
			var vertices = GetCentralCurve(accuracy);

			var numVertices = vertices.Length;

			result.VertexCount = numVertices;
			result.PointHeights = new float[numVertices];
			result.Cross = new bool[numVertices];
			result.PointRatios = new float[numVertices];
			result.MiddlePoints = new Vertex2D[numVertices];
			result.RgvLocal = new Vertex2D[_data.Type != RampType.RampTypeFlat ? (numVertices + 1) * 2 : numVertices * 2];

			// Compute an approximation to the length of the central curve
			// by adding up the lengths of the line segments.
			var totalLength = 0f;
			var bottomHeight = _data.HeightBottom + tableHeight;
			var topHeight = _data.HeightTop + tableHeight;

			for (var i = 0; i < numVertices - 1; i++) {
				var v1 = vertices[i];
				var v2 = vertices[i + 1];

				var dx = v1.X - v2.X;
				var dy = v1.Y - v2.Y;
				var length = MathF.Sqrt(dx * dx + dy * dy);

				totalLength += length;
			}

			var currentLength = 0f;
			for (var i = 0; i < numVertices; i++) {
				// clamp next and prev as ramps do not loop
				var prev = vertices[i > 0 ? i - 1 : i];
				var next = vertices[i < numVertices - 1 ? i + 1 : i];
				var middle = vertices[i];

				result.Cross[i] = middle.IsControlPoint;

				var normal = new Vertex2D();
				// Get normal at this point
				// Notice that these values equal the ones in the line
				// equation and could probably be substituted by them.
				var v1Normal = new Vertex2D(prev.Y - middle.Y, middle.X - prev.X); // vector vmiddle-vprev rotated RIGHT
				var v2Normal = new Vertex2D(middle.Y - next.Y, next.X - middle.X); // vector vnext-vmiddle rotated RIGHT

				// special handling for beginning and end of the ramp, as ramps do not loop
				if (i == numVertices - 1) {
					v1Normal.Normalize();
					normal = v1Normal;

				} else if (i == 0) {
					v2Normal.Normalize();
					normal = v2Normal;

				} else {
					v1Normal.Normalize();
					v2Normal.Normalize();

					if (MathF.Abs(v1Normal.X - v2Normal.X) < 0.0001 && MathF.Abs(v1Normal.Y - v2Normal.Y) < 0.0001) {
						// Two parallel segments
						normal = v1Normal;

					} else {
						// Find intersection of the two edges meeting this points, but
						// shift those lines outwards along their normals

						// First line
						var a = prev.Y - middle.Y;
						var b = middle.X - prev.X;

						// Shift line along the normal
						var c = -(a * (prev.X - v1Normal.X) + b * (prev.Y - v1Normal.Y));

						// Second line
						var d = next.Y - middle.Y;
						var e = middle.X - next.X;

						// Shift line along the normal
						var f = -(d * (next.X - v2Normal.X) + e * (next.Y - v2Normal.Y));

						var det = a * e - b * d;
						var invDet = det != 0.0 ? 1.0f / det : 0.0f;

						var intersectX = (b * f - e * c) * invDet;
						var intersectY = (c * d - a * f) * invDet;

						normal.X = middle.X - intersectX;
						normal.Y = middle.Y - intersectY;
					}
				}

				// Update current length along the ramp.
				var dx = prev.X - middle.X;
				var dy = prev.Y - middle.Y;
				var length = MathF.Sqrt(dx * dx + dy * dy);

				currentLength += length;

				var percentage = currentLength / totalLength;
				var currentWidth = percentage * (_data.WidthTop - _data.WidthBottom) + _data.WidthBottom;
				result.PointHeights[i] = middle.Z + percentage * (topHeight - bottomHeight) + bottomHeight;

				AssignHeightToControlPoint(vertices[i].Id, middle.Z + percentage * (topHeight - bottomHeight) + bottomHeight);
				result.PointRatios[i] = 1.0f - percentage;

				// only change the width if we want to create vertices for rendering or for the editor
				// the collision engine uses flat type ramps
				if (IsHabitrail() && _data.Type != RampType.RampType1Wire) {
					currentWidth = _data.WireDistanceX;
					if (incWidth) {
						currentWidth += 20.0f;
					}

				} else if (_data.Type == RampType.RampType1Wire) {
					currentWidth = _data.WireDiameter;
				}

				result.MiddlePoints[i] = new Vertex2D(middle.X, middle.Y) + normal;
				result.RgvLocal[i] = new Vertex2D(middle.X, middle.Y) + currentWidth * 0.5f * normal;
				result.RgvLocal[numVertices * 2 - i - 1] = new Vertex2D(middle.X, middle.Y) - currentWidth * 0.5f * normal;
			}

			return result;
		}

		public RenderVertex3D[] GetCentralCurve(float acc = -1.0f)
		{
			float accuracy;

			// todo might be able to remove in the future
			if (_data.DragPoints.Length > 0 && _data.DragPoints[0].Id == null) {
				foreach (var dp in _data.DragPoints) {
					dp.AssertId();
				}
			}

			// as solid ramps are rendered into the static buffer, always use maximum precision
			if (acc != -1.0) {
				accuracy = acc; // used for hit shape calculation, always!

			} else {
				// var mat = table.GetMaterial(_data.Material);
				// if (mat == null || !mat.IsOpacityActive) {
					accuracy = 10.0f;

				// } else {
				// 	accuracy = table.GetDetailLevel();
				// }
			}

			// min = 4 (highest accuracy/detail level), max = 4 * 10^(10/1.5) = ~18.000.000 (lowest accuracy/detail level)
			accuracy = 4.0f * MathF.Pow(10.0f, (10.0f - accuracy) * (1.0f / 1.5f));
			return DragPoint.GetRgVertex<RenderVertex3D, CatmullCurve3DCatmullCurveFactory>(_data.DragPoints, false, accuracy);
		}

		private bool IsHabitrail()
		{
			return _data.Type == RampType.RampType4Wire
			    || _data.Type == RampType.RampType1Wire
			    || _data.Type == RampType.RampType2Wire
			    || _data.Type == RampType.RampType3WireLeft
			    || _data.Type == RampType.RampType3WireRight;
		}

		private void AssignHeightToControlPoint(string id, float height)
		{
			var dp = _data.DragPoints.FirstOrDefault(dp => dp.Id == id);
			if (dp != null) {
				dp.CalcHeight = height;
			}
		}
	}

	public class RampVertex
	{
		public int VertexCount;
		public float[] PointHeights;
		public bool[] Cross;
		public float[] PointRatios;
		public Vertex2D[] MiddlePoints;
		public Vertex2D[] RgvLocal;
	}
}
