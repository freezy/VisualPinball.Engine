using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Flipper;

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

		public TableBuilder AddFlipper(string name)
		{
			var data = new FlipperData($"GameItem{_gameItem++}") {
				Name = name, Center = new Vertex2D(500, 500)
			};

			_table.Add(new Flipper.Flipper(data));
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
