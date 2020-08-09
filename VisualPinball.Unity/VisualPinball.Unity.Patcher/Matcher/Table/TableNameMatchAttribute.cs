namespace VisualPinball.Unity.Patcher.Matcher.Table
{
	/// <summary>
	/// Matches by table name (how the table is called in the script). <p/>
	/// </summary>
	public class TableNameMatchAttribute : TableMatchAttribute
	{
		private readonly string _name;

		public TableNameMatchAttribute(string name)
		{
			_name = name;
		}

		public override bool Matches(Engine.VPT.Table.Table table, string fileName)
		{
			return _name == null || table.Data.Name == _name;
		}
	}
}
