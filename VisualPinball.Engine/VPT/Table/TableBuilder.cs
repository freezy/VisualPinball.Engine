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

using System.Linq;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Trough;

namespace VisualPinball.Engine.VPT.Table
{
	public class TableBuilder
	{
		private static int _tableItem;
		private int _gameItem = 0;

		private readonly Table _table = new Table(new TableData());

		public TableBuilder()
		{
			_table.Data.Name = $"Table${_tableItem++}";
		}

		public TableBuilder WithTableScript(string vbs)
		{
			_table.Data.Code = vbs;
			return this;
		}

		public TableBuilder AddBumper(string name)
		{
			var data = new BumperData($"GameItem{_gameItem++}") {
				Name = name,
				Center = new Vertex2D(500, 500)
			};

			_table.Add(new Bumper.Bumper(data));
			return this;
		}

		public TableBuilder AddMaterial(Material material)
		{
			var mats = _table.Data.Materials.ToList();
			mats.Add(material);
			_table.Data.Materials = mats.ToArray();
			_table.Data.NumMaterials = mats.Count;

			return this;
		}

		public TableBuilder AddFlipper(string name)
		{
			var data = new FlipperData($"GameItem{_gameItem++}") {
				Name = name, Center = new Vertex2D(500, 500)
			};

			_table.Add(new Flipper.Flipper(data));
			return this;
		}

		public TableBuilder AddTrough(string name)
		{
			var data = new TroughData($"GameItem{_gameItem++}") {
				Name = name
			};

			_table.Add(new Trough.Trough(data));
			return this;
		}

		public Table Build(string name = null)
		{
			if (name != null) {
				_table.Data.Name = name;
			}
			return _table;
		}
	}
}
