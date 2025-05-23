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

using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Table
{
	/// <summary>
	/// The root object for everything table related. <p/>
	///
	/// A table contains all the playfield elements, as well as a set of
	/// global data.
	/// </summary>
	public class Table : Item<TableData>, IRenderable
	{
		public override string ItemGroupName => "Playfield";

		public float Width => Data.Right - Data.Left;
		public float Height => Data.Bottom - Data.Top;

		public float GlassHeight => Data.GlassHeight;

		private readonly TableContainer _tableContainer;
		private readonly TableMeshGenerator _meshGenerator;

		public Table(TableContainer tableContainer, TableData data) : base(data)
		{
			_tableContainer = tableContainer;
			_meshGenerator = new TableMeshGenerator(data);
		}

		public float GetSurfaceHeight(string surfaceName, float x, float y)
		{
			if (string.IsNullOrEmpty(surfaceName)) {
				return 0;
			}

			if (_tableContainer.Has<Surface.Surface>(surfaceName)) {
				return _tableContainer.Get<Surface.Surface>(surfaceName).Data.HeightTop;
			}

			if (_tableContainer.Has<Ramp.Ramp>(surfaceName)) {
				return _tableContainer.Get<Ramp.Ramp>(surfaceName).GetSurfaceHeight(x, y, this);
			}
			return 0;
		}

		public int GetDetailLevel()
		{
			return 10; // TODO
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table table, Origin origin) => Matrix3D.Identity;
		public Mesh GetMesh(string id, Table table, Origin origin = Origin.Global, bool asRightHanded = true)
			=> _meshGenerator.GetMesh(table, asRightHanded);

		public PbrMaterial GetMaterial(string id, Table table) => _meshGenerator.GetMaterial(table);

		#endregion

		#region Container Shortcuts

		public Material GetMaterial(string name) => _tableContainer.GetMaterial(name);
		public Texture GetTexture(string dataImage) => _tableContainer.GetTexture(dataImage);
		public string GetNewName<T>(string name) where T : IItem => _tableContainer.GetNewName<T>(name);

		#endregion
	}
}
