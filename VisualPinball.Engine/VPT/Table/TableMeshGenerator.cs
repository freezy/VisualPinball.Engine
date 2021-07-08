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

		private readonly ITableContainer _tableContainer;
		private Primitive.Primitive _playfield;

		public TableMeshGenerator(ITableContainer tableContainer)
		{
			_tableContainer = tableContainer;
		}

		public RenderObject GetRenderObject(bool asRightHanded = true)
		{
			var material = new PbrMaterial(_tableContainer.GetMaterial(_tableContainer.Table.Data.PlayfieldMaterial), _tableContainer.GetTexture(_tableContainer.Table.Data.Image));
			return GetFromTableDimensions(asRightHanded, material);
		}

		public RenderObjectGroup GetRenderObjects(Table table, Origin origin, bool asRightHanded = true)
		{
			var material = new PbrMaterial(table.GetMaterial(_tableContainer.Table.Data.PlayfieldMaterial), table.GetTexture(_tableContainer.Table.Data.Image));
			return HasMeshAsPlayfield
				? _playfield.GetRenderObjects(table, origin, asRightHanded, "Table", material)
				: new RenderObjectGroup(_tableContainer.Table.Data.Name, "Table", Matrix3D.Identity, GetFromTableDimensions(asRightHanded, material));
		}

		public void SetFromPrimitive(Primitive.Primitive primitive)
		{
			_playfield = primitive;
		}

		private RenderObject GetFromTableDimensions(bool asRightHanded, PbrMaterial material)
		{
			var rgv = new[] {
				new Vertex3DNoTex2(_tableContainer.Table.Data.Left, _tableContainer.Table.Data.Top, _tableContainer.Table.TableHeight),
				new Vertex3DNoTex2(_tableContainer.Table.Data.Right, _tableContainer.Table.Data.Top, _tableContainer.Table.TableHeight),
				new Vertex3DNoTex2(_tableContainer.Table.Data.Right, _tableContainer.Table.Data.Bottom, _tableContainer.Table.TableHeight),
				new Vertex3DNoTex2(_tableContainer.Table.Data.Left, _tableContainer.Table.Data.Bottom, _tableContainer.Table.TableHeight),
			};
			var mesh = new Mesh {
				Name = _tableContainer.Table.Data.Name,
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

			return new RenderObject(
				_tableContainer.Table.Data.Name,
				asRightHanded ? mesh.Transform(Matrix3D.RightHanded) : mesh,
				material,
				true
			);
		}
	}
}
