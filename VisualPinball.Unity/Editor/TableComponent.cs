using UnityEngine;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Editor
{
	public class TableComponent : ItemComponent<Table, TableData>
	{
		public Table Table => Item;

		protected override Table GetItem(TableData d)
		{
			return new Table(d);
		}

		protected override void OnDataSet()
		{
		}

		protected override string[] GetChildren()
		{
			return null;
		}
	}
}
