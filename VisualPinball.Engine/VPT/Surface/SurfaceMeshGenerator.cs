using System.Linq;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Surface
{

	public class SurfaceMeshGenerator {

		/**
		 * Returns the mesh of the surface.
		 * @see Surface::GenerateMesh
		 */
		public Mesh[] GenerateMeshes(SurfaceData data, Table.Table table) {

			var topMesh = new Mesh("Top");
			var sideMesh = new Mesh("Side");

			var vVertex = DragPoint.GetRgVertex<RenderVertex>(data.DragPoints, () => new RenderVertex(), CatmullCurve2D.FromVertex2D as any);
			var rgTexCoord = DragPoint.GetTextureCoords(data.DragPoints, vVertex);

			var numVertices = vVertex.Length;
			var rgNormal = new Vertex2D[numVertices];

			for (var i = 0; i < numVertices; i++) {

				var pv1 = vVertex[i];
				var pv2 = vVertex[(i < numVertices - 1) ? (i + 1) : 0];
				var dx = pv1.X - pv2.X;
				var dy = pv1.Y - pv2.Y;

				var invLen = 1.0f / MathF.Sqrt(dx * dx + dy * dy);

				rgNormal[i] = new Vertex2D {X = dy * invLen, Y = dx * invLen};
			}

			var bottom = data.HeightBottom * table.GetScaleZ() + table.GetTableHeight();
			var top = data.HeightTop * table.GetScaleZ() + table.GetTableHeight();

			var offset = 0;

			// Render side
			for (var i = 0; i < numVertices; i++) {

				var pv1 = vVertex[i];
				var pv2 = vVertex[(i < numVertices - 1) ? (i + 1) : 0];

				var a = (i == 0) ? (numVertices - 1) : (i - 1);
				var c = (i < numVertices - 1) ? (i + 1) : 0;

				var vNormal = new []{new Vertex2D(), new Vertex2D()};
				if (pv1.FSmooth) {
					vNormal[0].X = (rgNormal[a].X + rgNormal[i].X) * 0.5f;
					vNormal[0].Y = (rgNormal[a].Y + rgNormal[i].Y) * 0.5f;
				} else {
					vNormal[0].X = rgNormal[i].X;
					vNormal[0].Y = rgNormal[i].Y;
				}

				if (pv2.FSmooth) {
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

				if (data.SideImage != null) {
					sideMesh.Vertices[offset].Tu = rgTexCoord[i];
					sideMesh.Vertices[offset].Tv = 1.0f;

					sideMesh.Vertices[offset + 1].Tu = rgTexCoord[i];
					sideMesh.Vertices[offset + 1].Tv = 0f;

					sideMesh.Vertices[offset + 2].Tu = rgTexCoord[c];
					sideMesh.Vertices[offset + 2].Tv = 0f;

					sideMesh.Vertices[offset + 3].Tu = rgTexCoord[c];
					sideMesh.Vertices[offset + 3].Tv = 1.0f;
				}

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
			for (var i = 0; i < numVertices; i++) {
				sideMesh.Indices[i * 6] = offset2;
				sideMesh.Indices[i * 6 + 1] = offset2 + 1;
				sideMesh.Indices[i * 6 + 2] = offset2 + 2;
				sideMesh.Indices[i * 6 + 3] = offset2;
				sideMesh.Indices[i * 6 + 4] = offset2 + 2;
				sideMesh.Indices[i * 6 + 5] = offset2 + 3;

				offset2 += 4;
			}

			// draw top
			var vPoly = new int[numVertices];
			for (var i = 0; i < numVertices; i++) {
				vPoly[i] = i;
			}

			topMesh.Indices = Mesh.PolygonToTriangles(vVertex, vPoly);

			var numPolys = topMesh.Indices.Length / 3;
			if (numPolys == 0) {
				// no polys to render leave vertex buffer undefined
				return {};
			}

			var heightNotDropped = data.HeightTop * table.GetScaleZ();
			var heightDropped = data.HeightBottom * table.GetScaleZ() + 0.1;

			var dim = table.GetDimensions();
			var invTableWidth = 1.0f / dim.Width;
			var invTableHeight = 1.0f / dim.Height;

			Vertex3DNoTex2[][] vertsTop = { new Vertex3DNoTex2[numVertices], new Vertex3DNoTex2[numVertices], new Vertex3DNoTex2[numVertices]};
			for (var i = 0; i < numVertices; i++) {

				var pv0 = vVertex[i];

				vertsTop[0][i] = new Vertex3DNoTex2 {
					X = pv0.X,
					Y = pv0.Y,
					Z = heightNotDropped + table.GetTableHeight(),
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
					Z = data.HeightBottom,
					Tu = pv0.X * invTableWidth,
					Tv = pv0.Y * invTableHeight,
					Nx = 0,
					Ny = 0,
					Nz = -1.0f
				};
			}
			topMesh.Vertices = vertsTop[0];

			var meshes = new Mesh[0];
			if (topMesh.Vertices.Length > 0) {
				meshes = meshes.Concat(new[] {topMesh}).ToArray();
			}
			if (System.Math.Abs(top - bottom) > 0.00001f) {
				meshes = meshes.Concat(new[] {sideMesh}).ToArray();
			}
			return meshes;
		}
	}

}
