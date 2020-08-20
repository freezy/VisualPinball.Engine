using System;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Primitive
{
	public class PrimitiveMeshGenerator : MeshGenerator
	{
		private readonly PrimitiveData _data;

		protected override Vertex3D Position => _data.Position;
		protected override Vertex3D Scale => _data.Size;
		protected override float RotationZ => _data.RotAndTra[5];

		public PrimitiveMeshGenerator(PrimitiveData data)
		{
			_data = data;
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded = true,
			string parent = null, PbrMaterial material = null)
		{
			var (preVertexMatrix, preNormalsMatrix) = GetPreMatrix(table, origin, asRightHanded);
			var postMatrix = GetPostMatrix(table, origin);
			return new RenderObjectGroup(_data.Name, parent ?? "Primitives", postMatrix, new RenderObject(
				_data.Name,
				GetMesh().Transform(preVertexMatrix, preNormalsMatrix),
				material ?? new PbrMaterial(
					table.GetMaterial(_data.Material),
					table.GetTexture(_data.Image),
					table.GetTexture(_data.NormalMap)
				),
				_data.IsVisible
			));
		}

		public Mesh GetMesh()
		{
			return !_data.Use3DMesh ? CalculateBuiltinOriginal() : _data.Mesh.Clone();
		}

		protected override float BaseHeight(Table.Table table)
		{
			return table.TableHeight;
		}

		protected override Tuple<Matrix3D, Matrix3D> GetTransformationMatrix(Table.Table table)
		{
			// scale matrix
			var scaleMatrix = new Matrix3D();
			scaleMatrix.SetScaling(Scale.X, Scale.Y, Scale.Z);

			// translation matrix
			var transMatrix = new Matrix3D();
			transMatrix.SetTranslation(Position.X, Position.Y, Position.Z + table.TableHeight);

			// translation + rotation matrix
			var rotTransMatrix = new Matrix3D();
			rotTransMatrix.SetTranslation(_data.RotAndTra[3], _data.RotAndTra[4], _data.RotAndTra[5]);

			var tempMatrix = new Matrix3D();
			tempMatrix.RotateZMatrix(MathF.DegToRad(_data.RotAndTra[2]));
			rotTransMatrix.Multiply(tempMatrix);
			tempMatrix.RotateYMatrix(MathF.DegToRad(_data.RotAndTra[1]));
			rotTransMatrix.Multiply(tempMatrix);
			tempMatrix.RotateXMatrix(MathF.DegToRad(_data.RotAndTra[0]));
			rotTransMatrix.Multiply(tempMatrix);

			tempMatrix.RotateZMatrix(MathF.DegToRad(_data.RotAndTra[8]));
			rotTransMatrix.Multiply(tempMatrix);
			tempMatrix.RotateYMatrix(MathF.DegToRad(_data.RotAndTra[7]));
			rotTransMatrix.Multiply(tempMatrix);
			tempMatrix.RotateXMatrix(MathF.DegToRad(_data.RotAndTra[6]));
			rotTransMatrix.Multiply(tempMatrix);

			var fullMatrix = scaleMatrix.Clone();
			fullMatrix.Multiply(rotTransMatrix);
			fullMatrix.Multiply(transMatrix);  // fullMatrix = Smatrix * RTmatrix * Tmatrix
			scaleMatrix.SetScaling(1.0f, 1.0f, table.GetScaleZ());
			fullMatrix.Multiply(scaleMatrix);

			return new Tuple<Matrix3D, Matrix3D>(fullMatrix, null);
		}


		private Mesh CalculateBuiltinOriginal()
		{
			var mesh = new Mesh(_data.Name);

			// this recalculates the Original Vertices -> should be only called, when sides are altered.
			var outerRadius = -0.5f / MathF.Cos(MathF.PI / _data.Sides);
			var addAngle = 2.0f * MathF.PI / _data.Sides;
			var offsAngle = MathF.PI / _data.Sides;
			var minX = Constants.FloatMax;
			var minY = Constants.FloatMax;
			var maxX = -Constants.FloatMax;
			var maxY = -Constants.FloatMax;

			mesh.Vertices = new Vertex3DNoTex2[4 * _data.Sides + 2];

			// middle point top
			mesh.Vertices[0] = new Vertex3DNoTex2 {X = 0.0f, Y = 0.0f, Z = 0.5f};
			// middle point bottom
			mesh.Vertices[_data.Sides + 1] = new Vertex3DNoTex2 {X = 0.0f, Y = 0.0f, Z = -0.5f};

			for (var i = 0; i < _data.Sides; ++i) {
				var currentAngle = addAngle * i + offsAngle;

				// calculate Top
				var topVert = new Vertex3DNoTex2 { // top point at side
					X = MathF.Sin(currentAngle) * outerRadius,
					Y = MathF.Cos(currentAngle) * outerRadius,
					Z = 0.5f
				};
				mesh.Vertices[i + 1] = topVert;

				// calculate bottom
				var bottomVert = new Vertex3DNoTex2 { // bottom point at side
					X = topVert.X,
					Y = topVert.Y,
					Z = -0.5f
				};
				mesh.Vertices[i + 1 + _data.Sides + 1] = bottomVert;

				// calculate sides
				mesh.Vertices[_data.Sides * 2 + 2 + i] = topVert.Clone(); // sideTopVert
				mesh.Vertices[_data.Sides * 3 + 2 + i] = bottomVert.Clone(); // sideBottomVert

				// calculate bounds for X and Y
				if (topVert.X < minX) {
					minX = topVert.X;
				}

				if (topVert.X > maxX) {
					maxX = topVert.X;
				}

				if (topVert.Y < minY) {
					minY = topVert.Y;
				}

				if (topVert.Y > maxY) {
					maxY = topVert.Y;
				}
			}

			// these have to be replaced for image mapping
			var middle = mesh.Vertices[0]; // middle point top
			middle.Tu = 0.25f; // /4
			middle.Tv = 0.25f; // /4
			middle = mesh.Vertices[_data.Sides + 1]; // middle point bottom
			middle.Tu = 0.25f * 3.0f; // /4*3
			middle.Tv = 0.25f; // /4
			var invX = 0.5f / (maxX - minX);
			var invY = 0.5f / (maxY - minY);
			var invS = 1.0f / _data.Sides;

			for (var i = 0; i < _data.Sides; i++) {
				var topVert = mesh.Vertices[i + 1]; // top point at side
				topVert.Tu = (topVert.X - minX) * invX;
				topVert.Tv = (topVert.Y - minY) * invY;

				var bottomVert = mesh.Vertices[i + 1 + _data.Sides + 1]; // bottom point at side
				bottomVert.Tu = topVert.Tu + 0.5f;
				bottomVert.Tv = topVert.Tv;

				var sideTopVert = mesh.Vertices[_data.Sides * 2 + 2 + i];
				var sideBottomVert = mesh.Vertices[_data.Sides * 3 + 2 + i];

				sideTopVert.Tu = i * invS;
				sideTopVert.Tv = 0.5f;
				sideBottomVert.Tu = sideTopVert.Tu;
				sideBottomVert.Tv = 1.0f;
			}

			// So how many indices are needed?
			// 3 per Triangle top - we have m_sides triangles -> 0, 1, 2, 0, 2, 3, 0, 3, 4, ...
			// 3 per Triangle bottom - we have m_sides triangles
			// 6 per Side at the side (two triangles form a rectangle) - we have m_sides sides
			// == 12 * m_sides
			// * 2 for both cullings (m_DrawTexturesInside == true)
			// == 24 * m_sides
			// this will also be the initial sorting, when depths, Vertices and Indices are recreated, because calculateRealTimeOriginal is called.

			// 2 restore indices
			//   check if anti culling is enabled:
			if (_data.DrawTexturesInside) {
				mesh.Indices = new int[_data.Sides * 24];
				// draw yes everything twice
				// restore indices
				for (var i = 0; i < _data.Sides; i++) {
					var tmp = i == _data.Sides - 1 ? 1 : i + 2; // wrapping around
					// top
					mesh.Indices[i * 6] = 0;
					mesh.Indices[i * 6 + 1] = i + 1;
					mesh.Indices[i * 6 + 2] = tmp;
					mesh.Indices[i * 6 + 3] = 0;
					mesh.Indices[i * 6 + 4] = tmp;
					mesh.Indices[i * 6 + 5] = i + 1;

					var tmp2 = tmp + 1;

					// bottom
					mesh.Indices[6 * (i + _data.Sides)] = _data.Sides + 1;
					mesh.Indices[6 * (i + _data.Sides) + 1] = _data.Sides + tmp2;
					mesh.Indices[6 * (i + _data.Sides) + 2] = _data.Sides + 2 + i;
					mesh.Indices[6 * (i + _data.Sides) + 3] = _data.Sides + 1;
					mesh.Indices[6 * (i + _data.Sides) + 4] = _data.Sides + 2 + i;
					mesh.Indices[6 * (i + _data.Sides) + 5] = _data.Sides + tmp2;

					// sides
					mesh.Indices[12 * (i + _data.Sides)] = _data.Sides * 2 + tmp2;
					mesh.Indices[12 * (i + _data.Sides) + 1] = _data.Sides * 2 + 2 + i;
					mesh.Indices[12 * (i + _data.Sides) + 2] = _data.Sides * 3 + 2 + i;
					mesh.Indices[12 * (i + _data.Sides) + 3] = _data.Sides * 2 + tmp2;
					mesh.Indices[12 * (i + _data.Sides) + 4] = _data.Sides * 3 + 2 + i;
					mesh.Indices[12 * (i + _data.Sides) + 5] = _data.Sides * 3 + tmp2;
					mesh.Indices[12 * (i + _data.Sides) + 6] = _data.Sides * 2 + tmp2;
					mesh.Indices[12 * (i + _data.Sides) + 7] = _data.Sides * 3 + 2 + i;
					mesh.Indices[12 * (i + _data.Sides) + 8] = _data.Sides * 2 + 2 + i;
					mesh.Indices[12 * (i + _data.Sides) + 9] = _data.Sides * 2 + tmp2;
					mesh.Indices[12 * (i + _data.Sides) + 10] = _data.Sides * 3 + tmp2;
					mesh.Indices[12 * (i + _data.Sides) + 11] = _data.Sides * 3 + 2 + i;
				}

			} else {
				// only no out-facing polygons
				// restore indices
				mesh.Indices = new int[_data.Sides * 12];
				for (var i = 0; i < _data.Sides; i++) {
					var tmp = i == _data.Sides - 1 ? 1 : i + 2; // wrapping around
					// top
					mesh.Indices[i * 3] = 0;
					mesh.Indices[i * 3 + 2] = i + 1;
					mesh.Indices[i * 3 + 1] = tmp;

					//SetNormal(mesh.Vertices[0], &mesh.Indices[i+3], 3); // see below

					var tmp2 = tmp + 1;
					// bottom
					mesh.Indices[3 * (i + _data.Sides)] = _data.Sides + 1;
					mesh.Indices[3 * (i + _data.Sides) + 1] = _data.Sides + 2 + i;
					mesh.Indices[3 * (i + _data.Sides) + 2] = _data.Sides + tmp2;

					//SetNormal(mesh.Vertices[0], &mesh.Indices[3*(i+_data.Sides)], 3); // see below

					// sides
					mesh.Indices[6 * (i + _data.Sides)] = _data.Sides * 2 + tmp2;
					mesh.Indices[6 * (i + _data.Sides) + 1] = _data.Sides * 3 + 2 + i;
					mesh.Indices[6 * (i + _data.Sides) + 2] = _data.Sides * 2 + 2 + i;
					mesh.Indices[6 * (i + _data.Sides) + 3] = _data.Sides * 2 + tmp2;
					mesh.Indices[6 * (i + _data.Sides) + 4] = _data.Sides * 3 + tmp2;
					mesh.Indices[6 * (i + _data.Sides) + 5] = _data.Sides * 3 + 2 + i;
				}
			}

			//SetNormal(mesh.Vertices[0], &mesh.Indices[0], m_mesh.NumIndices()); // SetNormal only works for plane polygons
			Mesh.ComputeNormals(mesh.Vertices, mesh.Vertices.Length, mesh.Indices, mesh.Indices.Length);

			return mesh;
		}
	}
}
