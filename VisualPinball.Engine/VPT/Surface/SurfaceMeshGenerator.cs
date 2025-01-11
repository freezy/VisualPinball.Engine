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

using System;
using System.Collections.Generic;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using MathF = VisualPinball.Engine.Math.MathF;

namespace VisualPinball.Engine.VPT.Surface
{
	public class SurfaceMeshGenerator
	{
		public const string Side = "Side";
		public const string Top = "Top";

		private readonly ISurfaceData _data;

		public SurfaceMeshGenerator(ISurfaceData data)
		{
			_data = data;
		}

		public Mesh GetMesh(string id, float tableWidth, float tableHeight, float zHeight, bool asRightHanded)
		{
			var mesh = GenerateMesh(id, tableWidth, tableHeight, zHeight);
			return asRightHanded ? mesh.Transform(Matrix3D.RightHanded) : mesh;
		}

		public PbrMaterial GetMaterial(string id, Table.Table table, SurfaceData data)
		{
			switch (id) {
				case Top:
					return new PbrMaterial(table.GetMaterial(data.TopMaterial), table.GetTexture(data.Image));
				case Side:
					return new PbrMaterial(table.GetMaterial(data.SideMaterial), table.GetTexture(data.SideImage));
			}
			throw new ArgumentException($"Unknown mesh ID \"{id}\".");
		}

		private Mesh GenerateMesh(string id, float tableWidth, float tableHeight, float zHeight)
		{
			switch (id) {
				case Top:
					return GenerateTopMesh(tableWidth, tableHeight, zHeight);
				case Side:
					return GenerateSideMesh(zHeight);
			}
			throw new ArgumentException($"Unknown mesh ID \"{id}\".");
		}

		private Mesh GenerateTopMesh(float tableWidth, float tableHeight, float zHeight) {

			var topMesh = new Mesh("Top");
			var vVertex = DragPoint.GetRgVertex<RenderVertex2D, CatmullCurve2DCatmullCurveFactory>(_data.DragPoints);

			var numVertices = vVertex.Length;
			var rgNormal = new Vertex2D[numVertices];

			for (var i = 0; i < numVertices; i++) {

				var pv1 = vVertex[i];
				var pv2 = vVertex[i < numVertices - 1 ? i + 1 : 0];
				var dx = pv1.X - pv2.X;
				var dy = pv1.Y - pv2.Y;

				if (dx != 0.0f || dy != 0.0f) {
					var invLen = 1.0f / MathF.Sqrt(dx * dx + dy * dy);
					rgNormal[i] = new Vertex2D { X = dy * invLen, Y = dx * invLen };
				} else {
					rgNormal[i] = new Vertex2D { X = 0.0f, Y = 0.0f };
				}
			}

			// draw top
			var vPoly = new List<int>(new int[numVertices]);
			for (var i = 0; i < numVertices; i++) {
				vPoly[i] = i;
			}

			topMesh.Indices = Mesh.PolygonToTriangles(vVertex, vPoly);
			var numPolys = topMesh.Indices.Length / 3;
			if (numPolys == 0) {
				// no polys to render leave vertex buffer undefined
				return null;
			}

			var heightNotDropped = _data.HeightTop;
			var heightDropped = _data.HeightBottom + 0.1;

			var invTableWidth = 1.0f / tableWidth;
			var invTableHeight = 1.0f / tableHeight;

			Vertex3DNoTex2[][] vertsTop = { new Vertex3DNoTex2[numVertices], new Vertex3DNoTex2[numVertices], new Vertex3DNoTex2[numVertices]};
			for (var i = 0; i < numVertices; i++) {

				var pv0 = vVertex[i];

				vertsTop[0][i] = new Vertex3DNoTex2 {
					X = pv0.X,
					Y = pv0.Y,
					Z = heightNotDropped + zHeight,
					Tu = pv0.X * invTableWidth,
					Tv = pv0.Y * invTableHeight,
					Nx = 0,
					Ny = 0,
					Nz = 1.0f
				};

				vertsTop[1][i] = new Vertex3DNoTex2 {
					X = pv0.X,
					Y = pv0.Y,
					Z = (float) heightDropped,
					Tu = pv0.X * invTableWidth,
					Tv = pv0.Y * invTableHeight,
					Nx = 0,
					Ny = 0,
					Nz = 1.0f
				};

				vertsTop[2][i] = new Vertex3DNoTex2 {
					X = pv0.X,
					Y = pv0.Y,
					Z = _data.HeightBottom,
					Tu = pv0.X * invTableWidth,
					Tv = pv0.Y * invTableHeight,
					Nx = 0,
					Ny = 0,
					Nz = -1.0f
				};
			}
			topMesh.Vertices = vertsTop[0];

			return topMesh;
		}

		private Mesh GenerateSideMesh(float playfieldHeight) {

			var sideMesh = new Mesh("Side");

			var vVertex = DragPoint.GetRgVertex<RenderVertex2D, CatmullCurve2DCatmullCurveFactory>(_data.DragPoints);
			var rgTexCoord = DragPoint.GetTextureCoords(_data.DragPoints, vVertex);

			var numVertices = vVertex.Length;
			var rgNormal = new Vertex2D[numVertices];

			for (var i = 0; i < numVertices; i++) {

				var pv1 = vVertex[i];
				var pv2 = vVertex[i < numVertices - 1 ? i + 1 : 0];
				var dx = pv1.X - pv2.X;
				var dy = pv1.Y - pv2.Y;

				if (dx != 0.0f || dy != 0.0f) {
					var invLen = 1.0f / MathF.Sqrt(dx * dx + dy * dy);
					rgNormal[i] = new Vertex2D { X = dy * invLen, Y = dx * invLen };
				} else {
					rgNormal[i] = new Vertex2D { X = 0.0f, Y = 0.0f };
				}
			}

			var bottom = _data.HeightBottom + playfieldHeight;
			var top = _data.HeightTop + playfieldHeight;

			var offset = 0;

			// Render side
			sideMesh.Vertices = new Vertex3DNoTex2[numVertices * 4];
			for (var i = 0; i < numVertices; i++) {

				var pv1 = vVertex[i];
				var pv2 = vVertex[i < numVertices - 1 ? i + 1 : 0];

				var a = i == 0 ? numVertices - 1 : i - 1;
				var c = i < numVertices - 1 ? i + 1 : 0;

				var vNormal = new []{new Vertex2D(), new Vertex2D()};
				if (pv1.Smooth) {
					vNormal[0].X = (rgNormal[a].X + rgNormal[i].X) * 0.5f;
					vNormal[0].Y = (rgNormal[a].Y + rgNormal[i].Y) * 0.5f;
				} else {
					vNormal[0].X = rgNormal[i].X;
					vNormal[0].Y = rgNormal[i].Y;
				}

				if (pv2.Smooth) {
					vNormal[1].X = (rgNormal[i].X + rgNormal[c].X) * 0.5f;
					vNormal[1].Y = (rgNormal[i].Y + rgNormal[c].Y) * 0.5f;
				} else {
					vNormal[1].X = rgNormal[i].X;
					vNormal[1].Y = rgNormal[i].Y;
				}

				vNormal[0].Normalize();
				vNormal[1].Normalize();

				sideMesh.Vertices[offset] = new Vertex3DNoTex2();
				sideMesh.Vertices[offset + 1] = new Vertex3DNoTex2();
				sideMesh.Vertices[offset + 2] = new Vertex3DNoTex2();
				sideMesh.Vertices[offset + 3] = new Vertex3DNoTex2();

				sideMesh.Vertices[offset].X = pv1.X;
				sideMesh.Vertices[offset].Y = pv1.Y;
				sideMesh.Vertices[offset].Z = bottom;
				sideMesh.Vertices[offset + 1].X = pv1.X;
				sideMesh.Vertices[offset + 1].Y = pv1.Y;
				sideMesh.Vertices[offset + 1].Z = top;
				sideMesh.Vertices[offset + 2].X = pv2.X;
				sideMesh.Vertices[offset + 2].Y = pv2.Y;
				sideMesh.Vertices[offset + 2].Z = top;
				sideMesh.Vertices[offset + 3].X = pv2.X;
				sideMesh.Vertices[offset + 3].Y = pv2.Y;
				sideMesh.Vertices[offset + 3].Z = bottom;

				sideMesh.Vertices[offset].Tu = rgTexCoord[i];
				sideMesh.Vertices[offset].Tv = 1.0f;

				sideMesh.Vertices[offset + 1].Tu = rgTexCoord[i];
				sideMesh.Vertices[offset + 1].Tv = 0f;

				sideMesh.Vertices[offset + 2].Tu = rgTexCoord[c];
				sideMesh.Vertices[offset + 2].Tv = 0f;

				sideMesh.Vertices[offset + 3].Tu = rgTexCoord[c];
				sideMesh.Vertices[offset + 3].Tv = 1.0f;

				sideMesh.Vertices[offset].Nx = vNormal[0].X;
				sideMesh.Vertices[offset].Ny = -vNormal[0].Y;
				sideMesh.Vertices[offset].Nz = 0f;

				sideMesh.Vertices[offset + 1].Nx = vNormal[0].X;
				sideMesh.Vertices[offset + 1].Ny = -vNormal[0].Y;
				sideMesh.Vertices[offset + 1].Nz = 0f;

				sideMesh.Vertices[offset + 2].Nx = vNormal[1].X;
				sideMesh.Vertices[offset + 2].Ny = -vNormal[1].Y;
				sideMesh.Vertices[offset + 2].Nz = 0f;

				sideMesh.Vertices[offset + 3].Nx = vNormal[1].X;
				sideMesh.Vertices[offset + 3].Ny = -vNormal[1].Y;
				sideMesh.Vertices[offset + 3].Nz = 0f;

				offset += 4;
			}

			// prepare index buffer for sides
			var offset2 = 0;
			sideMesh.Indices = new int[numVertices * 6];
			for (var i = 0; i < numVertices; i++) {
				sideMesh.Indices[i * 6] = offset2;
				sideMesh.Indices[i * 6 + 1] = offset2 + 1;
				sideMesh.Indices[i * 6 + 2] = offset2 + 2;
				sideMesh.Indices[i * 6 + 3] = offset2;
				sideMesh.Indices[i * 6 + 4] = offset2 + 2;
				sideMesh.Indices[i * 6 + 5] = offset2 + 3;

				offset2 += 4;
			}

			return sideMesh;
		}
	}
}
