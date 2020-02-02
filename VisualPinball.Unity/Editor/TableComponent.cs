using UnityEngine;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Editor
{
	public class TableComponent : MonoBehaviour
	{
		public TableData Data;

		public Table Table => _table ?? (_table = new Table(Data));

		private Table _table;
	}
}
