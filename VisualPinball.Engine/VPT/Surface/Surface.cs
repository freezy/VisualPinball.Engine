// Visual Pinball Engine
// Copyright (C) 2022 freezy and VPE Team
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

using System.IO;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Surface
{
	public class Surface : Item<SurfaceData>, IRenderable
	{
		public override string ItemGroupName => "Walls";

		private readonly SurfaceMeshGenerator _meshGenerator;

		public Surface(SurfaceData data) : base(data)
		{
			_meshGenerator = new SurfaceMeshGenerator(Data);
		}

		public Surface(BinaryReader reader, string itemName) : this(new SurfaceData(reader, itemName))
		{
		}

		public static Surface GetDefault(Table.Table table)
		{
			var surfaceData = new SurfaceData(
				table.GetNewName<Surface>("Wall"),
				new[] {
					new DragPointData(table.Width / 2f - 50f, table.Height / 2f - 50f),
					new DragPointData(table.Width / 2f - 50f, table.Height / 2f + 50f),
					new DragPointData(table.Width / 2f + 50f, table.Height / 2f + 50f),
					new DragPointData(table.Width / 2f + 50f, table.Height / 2f - 50f)
				}
			);
			return new Surface(surfaceData);
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin) => Matrix3D.Identity;

		public Mesh GetMesh(string id, Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
			=> _meshGenerator.GetMesh(id, table.Width, table.Height, table.TableHeight, asRightHanded);

		public PbrMaterial GetMaterial(string id, Table.Table table) => _meshGenerator.GetMaterial(id, table, Data);

		#endregion
	}
}
