// ReSharper disable CompareOfFloatsByEqualityOperator

using System;
using System.Collections.Generic;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Ramp
{
	public class RampMeshGenerator
	{
		private readonly RampData _data;

		public RampMeshGenerator(RampData data)
		{
			_data = data;
		}

		public RenderObject[] GetRenderObjects(Table.Table table)
		{
			var meshes = GenerateMeshes(table);
			var renderObjects = new List<RenderObject>();

			// wires
			for (var i = 1; i <= 4; i++) {
				var name = $"Wire{i}";
				if (meshes.ContainsKey(name)) {
					renderObjects.Add(GetRenderObject(table, meshes, name));
				}
			}

			// floor and walls
			foreach (var name in new[] { "Floor", "RightWall", "LeftWall" }) {
				if (meshes.ContainsKey(name)) {
					renderObjects.Add(GetRenderObject(table, meshes, name));
				}
			}

			return renderObjects.ToArray();
		}

		private RenderObject GetRenderObject(Table.Table table, Dictionary<string, Mesh> meshes, string name)
		{
			return new RenderObject(
				name: name,
				mesh: meshes[name].Transform(Matrix3D.RightHanded),
				material: table.GetMaterial(_data.Material),
				isVisible: _data.IsVisible
			);
		}

		public Dictionary<string, Mesh> GenerateMeshes(Table.Table table)
		{
			if (!IsHabitrail()) {
				return GenerateFlatMesh(table);
			}
			var meshes = new Dictionary<string, Mesh>();
			var (wireMeshA, wireMeshB) = GenerateWireMeshes(table);
			switch (_data.RampType) {
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

		private Dictionary<string, Mesh> GenerateFlatMesh(Table.Table table) {
			var meshes = new Dictionary<string, Mesh>();
			var rv = GetRampVertex(table, -1, true);

			meshes["Floor"] = GenerateFlatFloorMesh(table, rv);

			if (_data.RightWallHeightVisible > 0.0) {
				meshes["RightWall"] = GenerateFlatRightWall(table, rv);
			}

			if (_data.LeftWallHeightVisible > 0.0) {
				meshes["LeftWall"] = GenerateFlatLeftWall(table, rv);
			}

			return meshes;
		}

		private Mesh GenerateFlatFloorMesh(Table.Table table, RampVertex rv)
		{
			var rampVertex = rv.pcvertex;
			var rgHeight = rv.ppheight;
			var rgRatio = rv.ppratio;
			var dim = table.GetDimensions();
			var invTableWidth = 1.0f / dim.Width;
			var invTableHeight = 1.0f / dim.Height;
			var numVertices = rv.pcvertex * 2;

			var mesh = new Mesh("Floor") {
				Vertices = new Vertex3DNoTex2[numVertices],
				Indices = new int[numVertices * 6]
			};
			for (var i = 0; i < rampVertex; i++) {
				var rgv3d1 = new Vertex3DNoTex2();
				var rgv3d2 = new Vertex3DNoTex2();

				rgv3d1.X = rv.rgvLocal[i].X;
				rgv3d1.Y = rv.rgvLocal[i].Y;
				rgv3d1.Z = rgHeight[i] * table.GetScaleZ();

				rgv3d2.X = rv.rgvLocal[rampVertex * 2 - i - 1].X;
				rgv3d2.Y = rv.rgvLocal[rampVertex * 2 - i - 1].Y;
				rgv3d2.Z = rgv3d1.Z;

				if (_data.Image != null) {
					if (_data.ImageAlignment == RampImageAlignment.ImageModeWorld) {
						rgv3d1.Tu = rgv3d1.X * invTableWidth;
						rgv3d1.Tv = rgv3d1.Y * invTableHeight;
						rgv3d2.Tu = rgv3d2.X * invTableWidth;
						rgv3d2.Tv = rgv3d2.Y * invTableHeight;

					} else {
						rgv3d1.Tu = 1.0f;
						rgv3d1.Tv = rgRatio[i];
						rgv3d2.Tu = 0.0f;
						rgv3d2.Tv = rgRatio[i];
					}

				} else {
					rgv3d1.Tu = 0.0f;
					rgv3d1.Tv = 0.0f;
					rgv3d2.Tu = 0.0f;
					rgv3d2.Tv = 0.0f;
				}

				mesh.Vertices[i * 2] = rgv3d1;
				mesh.Vertices[i * 2 + 1] = rgv3d2;

				if (i == rampVertex - 1) {
					break;
				}

				mesh.Indices[i * 6] = i * 2;
				mesh.Indices[i * 6 + 1] = i * 2 + 1;
				mesh.Indices[i * 6 + 2] = i * 2 + 3;
				mesh.Indices[i * 6 + 3] = i * 2;
				mesh.Indices[i * 6 + 4] = i * 2 + 3;
				mesh.Indices[i * 6 + 5] = i * 2 + 2;
			}

			Mesh.ComputeNormals(mesh.Vertices, numVertices, mesh.Indices, (rampVertex - 1) * 6);
			return mesh;
		}

		private Mesh GenerateFlatLeftWall(Table.Table table, RampVertex rv)
		{
			var rampVertex = rv.pcvertex;
			var rgHeight = rv.ppheight;
			var rgRatio = rv.ppratio;
			var dim = table.GetDimensions();
			var invTableWidth = 1.0f / dim.Width;
			var invTableHeight = 1.0f / dim.Height;
			var numVertices = rampVertex * 2;

			var mesh = new Mesh("LeftWall") {
				Vertices = new Vertex3DNoTex2[numVertices],
				Indices = new int[numVertices * 6]
			};
			for (var i = 0; i < rampVertex; i++) {
				var rgv3d1 = new Vertex3DNoTex2();
				var rgv3d2 = new Vertex3DNoTex2();

				rgv3d1.X = rv.rgvLocal[rampVertex * 2 - i - 1].X;
				rgv3d1.Y = rv.rgvLocal[rampVertex * 2 - i - 1].Y;
				rgv3d1.Z = rgHeight[i] * table.GetScaleZ();

				rgv3d2.X = rgv3d1.X;
				rgv3d2.Y = rgv3d1.Y;
				rgv3d2.Z = (rgHeight[i] + _data.LeftWallHeightVisible) * table.GetScaleZ();

				if (_data.Image != null && _data.ImageWalls) {
					if (_data.ImageAlignment == RampImageAlignment.ImageModeWorld) {
						rgv3d1.Tu = rgv3d1.X * invTableWidth;
						rgv3d1.Tv = rgv3d1.Y * invTableHeight;

					} else {
						rgv3d1.Tu = 0;
						rgv3d1.Tv = rgRatio[i];
					}

					rgv3d2.Tu = rgv3d1.Tu;
					rgv3d2.Tv = rgv3d1.Tv;

				} else {
					rgv3d1.Tu = 0.0f;
					rgv3d1.Tv = 0.0f;
					rgv3d2.Tu = 0.0f;
					rgv3d2.Tv = 0.0f;
				}

				mesh.Vertices[i * 2] = rgv3d1;
				mesh.Vertices[i * 2 + 1] = rgv3d2;

				if (i == rampVertex - 1) {
					break;
				}

				mesh.Indices[i * 6] = i * 2;
				mesh.Indices[i * 6 + 1] = i * 2 + 1;
				mesh.Indices[i * 6 + 2] = i * 2 + 3;
				mesh.Indices[i * 6 + 3] = i * 2;
				mesh.Indices[i * 6 + 4] = i * 2 + 3;
				mesh.Indices[i * 6 + 5] = i * 2 + 2;
			}

			Mesh.ComputeNormals(mesh.Vertices, numVertices, mesh.Indices, (rampVertex - 1) * 6);
			return mesh;
		}

		private Mesh GenerateFlatRightWall(Table.Table table, RampVertex rv) {
			var rampVertex = rv.pcvertex;
			var rgHeight = rv.ppheight;
			var rgRatio = rv.ppratio;
			var dim = table.GetDimensions();
			var invTableWidth = 1.0f / dim.Width;
			var invTableHeight = 1.0f / dim.Height;
			var numVertices = rampVertex * 2;

			var mesh = new Mesh("RightWall") {
				Vertices = new Vertex3DNoTex2[numVertices],
				Indices = new int[numVertices * 6]
			};
			for (var i = 0; i < rampVertex; i++) {
				var rgv3d1 = new Vertex3DNoTex2();
				var rgv3d2 = new Vertex3DNoTex2();

				rgv3d1.X = rv.rgvLocal[i].X;
				rgv3d1.Y = rv.rgvLocal[i].Y;
				rgv3d1.Z = rgHeight[i] * table.GetScaleZ();

				rgv3d2.X = rv.rgvLocal[i].X;
				rgv3d2.Y = rv.rgvLocal[i].Y;
				rgv3d2.Z = (rgHeight[i] + _data.RightWallHeightVisible) * table.GetScaleZ();

				if (_data.Image != null && _data.ImageWalls) {
					if (_data.ImageAlignment == RampImageAlignment.ImageModeWorld) {
						rgv3d1.Tu = rgv3d1.X * invTableWidth;
						rgv3d1.Tv = rgv3d1.Y * invTableHeight;

					} else {
						rgv3d1.Tu = 0;
						rgv3d1.Tv = rgRatio[i];
					}
					rgv3d2.Tu = rgv3d1.Tu;
					rgv3d2.Tv = rgv3d1.Tv;

				} else {
					rgv3d1.Tu = 0.0f;
					rgv3d1.Tv = 0.0f;
					rgv3d2.Tu = 0.0f;
					rgv3d2.Tv = 0.0f;
				}

				mesh.Vertices[i * 2] = rgv3d1;
				mesh.Vertices[i * 2 + 1] = rgv3d2;

				if (i == rampVertex - 1) {
					break;
				}

				mesh.Indices[i * 6] = i * 2;
				mesh.Indices[i * 6 + 1] = i * 2 + 1;
				mesh.Indices[i * 6 + 2] = i * 2 + 3;
				mesh.Indices[i * 6 + 3] = i * 2;
				mesh.Indices[i * 6 + 4] = i * 2 + 3;
				mesh.Indices[i * 6 + 5] = i * 2 + 2;
			}

			Mesh.ComputeNormals(mesh.Vertices, numVertices, mesh.Indices, (rampVertex - 1) * 6);
			return mesh;
		}

		private Tuple<Mesh, Mesh> GenerateWireMeshes(Table.Table table)
		{
			int accuracy;
			if (table.GetDetailLevel() < 5) {
				accuracy = 6;

			} else if (table.GetDetailLevel() >= 5 && table.GetDetailLevel() < 8) {
				accuracy = 8;

			} else {
				accuracy = (int) (table.GetDetailLevel() * 1.3f); // see below
			}

			// as solid ramps are rendered into the static buffer, always use maximum precision
			var mat = table.GetMaterial(_data.Material);
			if (mat == null || !mat.IsOpacityActive)
			{
				accuracy = (int) (10.0f * 1.3f); // see above
			}

			var rv = GetRampVertex(table, -1, false);
			var splinePoints = rv.pcvertex;
			var rgheightInit = rv.ppheight;
			var middlePoints = rv.pMiddlePoints;

			var numRings = splinePoints;
			var numSegments = accuracy;
			var numVertices = numRings*numSegments;
			var numIndices = 6 * numVertices; //m_numVertices*2+2;

			var tmpPoints = new Vertex2D[splinePoints];

			for (var i = 0; i < splinePoints; i++) {
				tmpPoints[i] = rv.rgvLocal[splinePoints * 2 - i - 1];
			}

			Vertex3DNoTex2[] vertBuffer;
			Vertex3DNoTex2[] vertBuffer2;

			if (_data.RampType != RampType.RampType1Wire) {
				vertBuffer = CreateWire(numRings, numSegments, rv.rgvLocal, rgheightInit);
				vertBuffer2 = CreateWire(numRings, numSegments, tmpPoints, rgheightInit);

			} else {
				vertBuffer = CreateWire(numRings, numSegments, middlePoints, rgheightInit);
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

			if (_data.RampType != RampType.RampType1Wire) {
				return new Tuple<Mesh, Mesh>(
					new Mesh(vertBuffer, indices),
					new Mesh(vertBuffer2, indices)
				);
			}
			return new Tuple<Mesh, Mesh>(new Mesh(vertBuffer, indices), null);
		}

		private Vertex3DNoTex2[] CreateWire(int numRings, int numSegments, Vertex2D[] midPoints, float[] rgheightInit)
		{
			var rgvbuf = new Vertex3DNoTex2[numRings * numSegments];
			var prevB = new Vertex3D();
			var index = 0;
			for (var i = 0; i < numRings; i++)
			{
				var i2 = i == numRings - 1 ? i : i + 1;
				var height = rgheightInit[i];

				var tangent = new Vertex3D(midPoints[i2].X - midPoints[i].X, midPoints[i2].Y - midPoints[i].Y,
					rgheightInit[i2] - rgheightInit[i]);
				if (i == numRings - 1)
				{
					// for the last spline point use the previous tangent again, otherwise we won"t see the complete wire (it stops one control point too early)
					tangent.X = midPoints[i].X - midPoints[i - 1].X;
					tangent.Y = midPoints[i].Y - midPoints[i - 1].Y;
				}

				Vertex3D binorm;
				Vertex3D normal;
				if (i == 0)
				{
					var up = new Vertex3D(midPoints[i2].X + midPoints[i].X, midPoints[i2].Y + midPoints[i].Y,
						rgheightInit[i2] - height);
					normal = tangent.Clone().Cross(up); //normal
					binorm = tangent.Clone().Cross(normal);
				}
				else
				{
					normal = prevB.Clone().Cross(tangent);
					binorm = tangent.Clone().Cross(normal);
				}

				binorm.Normalize();
				normal.Normalize();
				prevB = binorm;

				var invNumRings = 1.0f / numRings;
				var invNumSegments = 1.0f / numSegments;
				var u = i * invNumRings;
				for (var j = 0; j < numSegments; j++, index++)
				{
					var v = (j + u) * invNumSegments;
					Vertex3D tmp = Vertex3D.GetRotatedAxis(j * (360.0f * invNumSegments), tangent, normal)
						.MultiplyScalar(_data.WireDiameter * 0.5f);
					rgvbuf[index] = new Vertex3DNoTex2();
					rgvbuf[index].X = midPoints[i].X + tmp.X;
					rgvbuf[index].Y = midPoints[i].Y + tmp.Y;
					rgvbuf[index].Z = height + tmp.Z;
					//texel
					rgvbuf[index].Tu = u;
					rgvbuf[index].Tv = v;
					var n = new Vertex3D(rgvbuf[index].X - midPoints[i].X, rgvbuf[index].Y - midPoints[i].Y,
						rgvbuf[index].Z - height);
					var len = 1.0f / MathF.Sqrt(n.X * n.X + n.Y * n.Y + n.Z * n.Z);
					rgvbuf[index].Nx = n.X * len;
					rgvbuf[index].Ny = n.Y * len;
					rgvbuf[index].Nz = n.Z * len;
				}
			}

			return rgvbuf;
		}

		public RampVertex GetRampVertex(Table.Table table, float accuracy, bool incWidth)
		{
			var result = new RampVertex();

			// vvertex are the 2D vertices forming the central curve of the ramp as seen from above
			var vvertex = GetCentralCurve(table, accuracy);

			var cvertex = vvertex.Length;

			result.pcvertex = cvertex;
			result.ppheight = new float[cvertex];
			result.ppfCross = new bool[cvertex];
			result.ppratio = new float[cvertex];
			result.pMiddlePoints = new Vertex2D[cvertex];
			result.rgvLocal = new Vertex2D[_data.RampType != RampType.RampTypeFlat ? (cvertex + 1) * 2 : cvertex * 2];

			// Compute an approximation to the length of the central curve
			// by adding up the lengths of the line segments.
			var totalLength = 0f;
			var bottomHeight = _data.HeightBottom + table.GetTableHeight();
			var topHeight = _data.HeightTop + table.GetTableHeight();

			for (var i = 0; i < cvertex - 1; i++) {
				var v1 = vvertex[i];
				var v2 = vvertex[i + 1];

				var dx = v1.X - v2.X;
				var dy = v1.Y - v2.Y;
				var length = MathF.Sqrt(dx * dx + dy * dy);

				totalLength = totalLength + length;
			}

			var currentLength = 0f;
			for (var i = 0; i < cvertex; i++) {
				// clamp next and prev as ramps do not loop
				var vprev = vvertex[i > 0 ? i - 1 : i];
				var vnext = vvertex[i < cvertex - 1 ? i + 1 : i];
				var vmiddle = vvertex[i];

				result.ppfCross[i] = vmiddle.IsControlPoint;

				var vnormal = new Vertex2D();
				// Get normal at this point
				// Notice that these values equal the ones in the line
				// equation and could probably be substituted by them.
				var v1normal = new Vertex2D(vprev.Y - vmiddle.Y, vmiddle.X - vprev.X); // vector vmiddle-vprev rotated RIGHT
				var v2normal = new Vertex2D(vmiddle.Y - vnext.Y, vnext.X - vmiddle.X); // vector vnext-vmiddle rotated RIGHT

				// special handling for beginning and end of the ramp, as ramps do not loop
				if (i == cvertex - 1) {
					v1normal.Normalize();
					vnormal = v1normal;

				} else if (i == 0) {
					v2normal.Normalize();
					vnormal = v2normal;

				} else {
					v1normal.Normalize();
					v2normal.Normalize();

					if (MathF.Abs(v1normal.X - v2normal.X) < 0.0001 && MathF.Abs(v1normal.Y - v2normal.Y) < 0.0001) {
						// Two parallel segments
						vnormal = v1normal;

					} else {
						// Find intersection of the two edges meeting this points, but
						// shift those lines outwards along their normals

						// First line
						var A = vprev.Y - vmiddle.Y;
						var B = vmiddle.X - vprev.X;

						// Shift line along the normal
						var C = -(A * (vprev.X - v1normal.X) + B * (vprev.Y - v1normal.Y));

						// Second line
						var D = vnext.Y - vmiddle.Y;
						var E = vmiddle.X - vnext.X;

						// Shift line along the normal
						var F = -(D * (vnext.X - v2normal.X) + E * (vnext.Y - v2normal.Y));

						var det = A * E - B * D;
						var invDet = det != 0.0 ? 1.0f / det : 0.0f;

						var intersectX = (B * F - E * C) * invDet;
						var intersectY = (C * D - A * F) * invDet;

						vnormal.X = vmiddle.X - intersectX;
						vnormal.Y = vmiddle.Y - intersectY;
					}
				}

				// Update current length along the ramp.
				var dx = vprev.X - vmiddle.X;
				var dy = vprev.Y - vmiddle.Y;
				var length = MathF.Sqrt(dx * dx + dy * dy);

				currentLength = currentLength + length;

				var percentage = currentLength / totalLength;
				var currentWidth = percentage * (_data.WidthTop - _data.WidthBottom) + _data.WidthBottom;
				result.ppheight[i] = vmiddle.Z + percentage * (topHeight - bottomHeight) + bottomHeight;

				AssignHeightToControlPoint(vvertex[i],
					vmiddle.Z + percentage * (topHeight - bottomHeight) + bottomHeight);
				result.ppratio[i] = 1.0f - percentage;

				// only change the width if we want to create vertices for rendering or for the editor
				// the collision engine uses flat type ramps
				if (IsHabitrail() && _data.RampType != RampType.RampType1Wire)
				{
					currentWidth = _data.WireDistanceX;
					if (incWidth)
					{
						currentWidth = currentWidth + 20.0f;
					}
				}
				else if (_data.RampType == RampType.RampType1Wire)
				{
					currentWidth = _data.WireDiameter;
				}

				result.pMiddlePoints[i] = new Vertex2D(vmiddle.X, vmiddle.Y).Add(vnormal);
				result.rgvLocal[i] =
					new Vertex2D(vmiddle.X, vmiddle.Y).Add(vnormal.Clone().MultiplyScalar(currentWidth * 0.5f));
				result.rgvLocal[cvertex * 2 - i - 1] =
					new Vertex2D(vmiddle.X, vmiddle.Y).Sub(vnormal.Clone().MultiplyScalar(currentWidth * 0.5f));
			}

			return result;
		}

		public RenderVertex3D[] GetCentralCurve(Table.Table table, float acc = -1.0f)
		{
			float accuracy;

			// as solid ramps are rendered into the static buffer, always use maximum precision
			if (acc != -1.0) {
				accuracy = acc; // used for hit shape calculation, always!

			} else {
				var mat = table.GetMaterial(_data.Material);
				if (mat == null || !mat.IsOpacityActive) {
					accuracy = 10.0f;

				} else {
					accuracy = table.GetDetailLevel();
				}
			}

			// min = 4 (highest accuracy/detail level), max = 4 * 10^(10/1.5) = ~18.000.000 (lowest accuracy/detail level)
			accuracy = 4.0f * MathF.Pow(10.0f, (10.0f - accuracy) * (1.0f / 1.5f));
			return DragPoint.GetRgVertex<RenderVertex3D, CatmullCurve3DCatmullCurveFactory>(_data.DragPoints, false, accuracy);
		}

		private bool IsHabitrail()
		{
			return _data.RampType == RampType.RampType4Wire
			    || _data.RampType == RampType.RampType1Wire
			    || _data.RampType == RampType.RampType2Wire
			    || _data.RampType == RampType.RampType3WireLeft
			    || _data.RampType == RampType.RampType3WireRight;
		}

		private void AssignHeightToControlPoint(Vertex2D v, float height)
		{
			foreach (var dragPoint in _data.DragPoints) {
				if (dragPoint.Vertex.X == v.X && dragPoint.Vertex.Y == v.Y) {
					dragPoint.CalcHeight = height;
				}
			}
		}
	}

	public class RampVertex
	{
		public int pcvertex;
		public float[] ppheight;
		public bool[] ppfCross;
		public float[] ppratio;
		public Vertex2D[] pMiddlePoints;
		public Vertex2D[] rgvLocal;
	}
}
