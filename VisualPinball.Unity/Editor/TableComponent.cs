using UnityEngine;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Editor
{
	public class TableComponent : ItemComponent<Table, TableData>
	{
		public Table Table => Item;

		protected override Table GetItem(TableData data)
		{
			return new Table(data);
		}

		protected override void OnDataSet(TableData data)
		{
		}
	}
}
