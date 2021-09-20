﻿// Visual Pinball Engine
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

using System.Linq;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Table
{
	public class TableMeshGenerator
	{
		public bool HasMeshAsPlayfield => _playfield != null;

		private readonly TableData _data;
		private Primitive.Primitive _playfield;

		public TableMeshGenerator(TableData data)
		{
			_data = data;
		}

		public Mesh GetMesh()
		{
			return GetFromTableDimensions(false);
		}

		public Mesh GetMesh(Table table, bool asRightHanded = true)
		{
			var mesh = GetFromTableDimensions(asRightHanded);
			return asRightHanded ? mesh.Transform(Matrix3D.RightHanded) : mesh;
		}

		public PbrMaterial GetMaterial(Table table)
		{
			return new PbrMaterial(table.GetMaterial(_data.PlayfieldMaterial), table.GetTexture(_data.Image));
		}

		public void SetFromPrimitive(Primitive.Primitive primitive)
		{
			_playfield = primitive;
		}

		private Mesh GetFromTableDimensions(bool asRightHanded)
		{
			var rgv = new[] {
				new Vertex3DNoTex2(_data.Left, _data.Top, _data.TableHeight),
				new Vertex3DNoTex2(_data.Right, _data.Top, _data.TableHeight),
				new Vertex3DNoTex2(_data.Right, _data.Bottom, _data.TableHeight),
				new Vertex3DNoTex2(_data.Left, _data.Bottom, _data.TableHeight),
			};
			var mesh = new Mesh {
				Name = _data.Name,
				Vertices = rgv.Select(r => new Vertex3DNoTex2()).ToArray(),
				Indices = new [] { 0, 1, 3, 0, 3, 2 }
			};

			for (var i = 0; i < 4; ++i) {
				rgv[i].Nx = 0;
				rgv[i].Ny = 0;
				rgv[i].Nz = 1.0f;

				rgv[i].Tv = (i & 2) > 0 ? 1.0f : 0.0f;
				rgv[i].Tu = i == 1 || i == 2 ? 1.0f : 0.0f;
			}

			var offs = 0;
			for (var y = 0; y <= 1; ++y) {
				for (var x = 0; x <= 1; ++x, ++offs) {
					mesh.Vertices[offs].X = (x & 1) > 0 ? rgv[1].X : rgv[0].X;
					mesh.Vertices[offs].Y = (y & 1) > 0 ? rgv[2].Y : rgv[0].Y;
					mesh.Vertices[offs].Z = rgv[0].Z;

					mesh.Vertices[offs].Tu = (x & 1) > 0 ? rgv[1].Tu : rgv[0].Tu;
					mesh.Vertices[offs].Tv = (y & 1) > 0 ? rgv[2].Tv : rgv[0].Tv;

					mesh.Vertices[offs].Nx = rgv[0].Nx;
					mesh.Vertices[offs].Ny = rgv[0].Ny;
					mesh.Vertices[offs].Nz = rgv[0].Nz;
				}
			}

			return mesh;
		}
	}
}
