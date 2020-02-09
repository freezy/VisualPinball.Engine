// ReSharper disable ClassNeverInstantiated.Global

using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Components
{
	public class VisualPinballTable : ItemComponent<Table, TableData>
	{
		public Table Table => Item;

		protected override Table GetItem(TableData d)
		{
			return new Table(d);
		}

		protected override void OnDataSet()
		{
		}

		protected override void OnFieldsUpdated()
		{
		}

		protected override string[] GetChildren()
		{
			return null;
		}
	}
}
