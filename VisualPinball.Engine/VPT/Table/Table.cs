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
		public override string ItemName { get; } = "Table";
		public override string ItemGroupName { get; } = "Playfield";
		public override ItemType ItemType { get; } = ItemType.Table;

		public float Width => Data.Right - Data.Left;
		public float Height => Data.Bottom - Data.Top;

		public float TableHeight => Data.TableHeight;

		public float GlassHeight => Data.GlassHeight;
		public Rect3D BoundingBox => new Rect3D(Data.Left, Data.Right, Data.Top, Data.Bottom, TableHeight, GlassHeight);

		public bool HasMeshAsPlayfield => _meshGenerator.HasMeshAsPlayfield;

		public Vertex3D Position { get => new Vertex3D(0, 0, 0); set { } }
		public float RotationY { get => 0; set { } }

		private readonly TableContainer _tableContainer;
		private readonly TableMeshGenerator _meshGenerator;

		public Table(TableContainer tableContainer, TableData data) : base(data)
		{
			_tableContainer = tableContainer;
			_meshGenerator = new TableMeshGenerator(data);
		}

		public float GetScaleZ()
		{
			return Data.BgScaleZ?[Data.BgCurrentSet] ?? 1.0f;
		}

		public float GetSurfaceHeight(string surfaceName, float x, float y)
		{
			if (string.IsNullOrEmpty(surfaceName)) {
				return TableHeight;
			}

			if (_tableContainer.Has<Surface.Surface>(surfaceName)) {
				return TableHeight + _tableContainer.Get<Surface.Surface>(surfaceName).Data.HeightTop;
			}

			if (_tableContainer.Has<Ramp.Ramp>(surfaceName)) {
				return TableHeight + _tableContainer.Get<Ramp.Ramp>(surfaceName).GetSurfaceHeight(x, y, this);
			}

			// Logger.Warn(
			// 	"[Table.getSurfaceHeight] Unknown surface {0}.\nAvailable surfaces: [ {1} ]\nAvailable ramps: [ {2} ]",
			// 	surfaceName,
			// 	string.Join(", ", _surfaces.Keys),
			// 	string.Join(", ", _ramps.Keys)
			// );
			return TableHeight;
		}

		public void SetupPlayfieldMesh()
		{
			if (_tableContainer.Has<Primitive.Primitive>("playfield_mesh")) {
				_meshGenerator.SetFromPrimitive(_tableContainer.Get<Primitive.Primitive>("playfield_mesh"));
				_tableContainer.Remove<Primitive.Primitive>("playfield_mesh");
			}
		}

		public int GetDetailLevel()
		{
			return 10; // TODO
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table table, Origin origin) => Matrix3D.Identity;

		public RenderObject GetRenderObject(Table table, string id = null, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObject(table, asRightHanded);
		}

		public RenderObjectGroup GetRenderObjects(Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		#endregion

		#region Container Shortcuts

		public Material GetMaterial(string name) => _tableContainer.GetMaterial(name);
		public Texture GetTexture(string dataImage) => _tableContainer.GetTexture(dataImage);
		public string GetNewName<T>(string name) where T : IItem => _tableContainer.GetNewName<T>(name);

		#endregion
	}
}

